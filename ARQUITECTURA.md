# Arquitectura de Software — PC2 Banco Arboleda (Recarga Celular)

> Documento de arquitectura para que el agente de IA genere el proyecto.
> Caso: **CU-03 Recarga celular** del Banco Arboleda (SI806 — Desarrollo Adaptativo).
> Stack: **C# / .NET** en backend y frontend.
> Prioridades del docente: que **funcione**, que se **levante rápido con `dotnet run`**, que **resista pruebas de estrés** (letras en campos numéricos, inyección SQL, datos inconsistentes, doble clic) y que el **cambio en vivo del sábado sea barato y fácil de entender**.
> Alcance: **únicamente la Historia de Usuario de Recarga Celular.** No hay login. No hay Pago de Servicio ni Transferencia.

---

## 1. Decisión de arquitectura (resumen)

**Backend y frontend desacoplados, ambos en C#/.NET, comunicándose por una API REST/JSON.**

| Capa | Tecnología elegida | Razón |
|------|--------------------|-------|
| Backend | **ASP.NET Core Web API** (.NET 10 LTS) | Se levanta con `dotnet run`. API REST limpia, separada del frontend. Mínimas dependencias. |
| Frontend | **Blazor WebAssembly** (.NET 10) | Todo en C# (cumple el requisito). Se levanta con `dotnet run`. UI por componentes, validaciones claras en un solo lugar, fácil de explicar y modificar en vivo. |
| Base de datos | **SQLite (archivo `.db` dentro del repo)** | Sin servidor, sin nube, sin internet, sin contraseñas. El profesor descarga el repo y `dotnet run` abre el archivo y funciona. Es exactamente "archivos gestionados sin que él haga nada". |
| Acceso a datos | **Microsoft.Data.Sqlite con consultas parametrizadas** (sin ORM / sin EF Core) | El ORM es una dependencia innecesaria aquí y oculta el SQL. Los parámetros (`@celular`, `@monto`) **neutralizan la inyección SQL** por diseño y dejan el SQL a la vista para el cambio en vivo. |

> **Por qué Web API + Blazor WASM y no otra cosa:**
> - **No MVC con Razor Pages:** mezclaría frontend y backend en un mismo proyecto; el profesor exige carpetas separadas de backend y frontend.
> - **No Angular/React:** no serían C#; el requisito es C# en ambos lados.
> - **No microservicios:** el caso es un único flujo (recarga). Añadirían complejidad de despliegue que encarece el "costo de cambio".
> - **No EF Core:** un ORM para una sola operación es sobre-ingeniería; `Microsoft.Data.Sqlite` directo es más simple de entender bajo presión y mantiene el SQL visible.
> - **No PostgreSQL en la nube:** cumpliría "cero configuración", pero dependería de internet el día de la evaluación (riesgo fuera de nuestro control) y expondría una cadena de conexión con contraseña en el repo. Innecesario para este alcance.
> - **No "todo en memoria":** perdería el estado al reiniciar el backend (el saldo se resetearía a mitad de prueba) y el caso pide "registrar la recarga", lo que implica persistencia. SQLite da persistencia real con la misma simplicidad de arranque.
> - **Por qué SQLite:** es un archivo dentro del repo; no hay servidor, internet ni secretos. Levanta tan fácil como la opción en memoria pero persiste de verdad, y conserva SQL parametrizado para defenderse de la inyección SQL.

---

## 2. Estructura de carpetas

Todo en una sola carpeta raíz con dos subcarpetas (como exige el docente):

```
banco-arboleda-pc2/
├── README.md                 # paso a paso para levantar todo
├── ARQUITECTURA.md           # este documento
├── backend/                  # ASP.NET Core Web API
│   ├── Backend.csproj
│   ├── Program.cs            # configuración de servicios, CORS, init de BD y arranque
│   ├── appsettings.json      # ruta del archivo SQLite
│   ├── schema.sql            # DDL + datos semilla (se ejecuta solo si la BD no existe)
│   ├── banco.db             # archivo SQLite versionado, ya poblado (ver nota)
│   ├── Controllers/
│   │   └── RecargasController.cs   # endpoints de recarga
│   ├── Services/
│   │   └── RecargaService.cs       # reglas de negocio (débito, idempotencia, transacción)
│   ├── Data/
│   │   └── Db.cs                   # fábrica de conexión SQLite + init del esquema
│   ├── Models/
│   │   ├── RecargaRequest.cs
│   │   └── RecargaResponse.cs
│   └── Validators/
│       └── RecargaValidator.cs     # validación de entrada centralizada
└── frontend/                 # Blazor WebAssembly
    ├── Frontend.csproj
    ├── Program.cs
    ├── wwwroot/
    │   └── appsettings.json        # URL base de la API
    ├── Pages/
    │   └── RecargaCelular.razor    # la HU completa, los pasos del caso
    ├── Services/
    │   └── RecargaApiClient.cs     # HttpClient tipado hacia la Web API
    └── Models/
        ├── RecargaRequest.cs
        └── RecargaResponse.cs
```

---

## 3. Modelo de datos (SQLite)

Esquema mínimo para sostener el flujo de recarga con saldo e idempotencia. Sin tabla de login (fuera de alcance). Sintaxis SQLite.

```sql
-- Cuenta del cliente (una sola, precargada; no hay login)
CREATE TABLE IF NOT EXISTS cuenta (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    titular TEXT NOT NULL,
    saldo   REAL NOT NULL DEFAULT 0 CHECK (saldo >= 0)
);

-- Operadores celulares válidos (catálogo controlado)
CREATE TABLE IF NOT EXISTS operador (
    id     INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre TEXT UNIQUE NOT NULL
);

-- Recargas realizadas (auditoría + idempotencia)
CREATE TABLE IF NOT EXISTS recarga (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    cuenta_id       INTEGER NOT NULL REFERENCES cuenta(id),
    celular         TEXT NOT NULL CHECK (length(celular) = 9),
    operador_id     INTEGER NOT NULL REFERENCES operador(id),
    monto           REAL NOT NULL CHECK (monto > 0),
    idempotency_key TEXT UNIQUE NOT NULL,   -- evita doble recarga por doble clic
    creado_en       TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Datos semilla (solo si están vacías)
INSERT INTO operador (nombre)
SELECT v FROM (SELECT 'Claro' v UNION SELECT 'Movistar' UNION SELECT 'Entel' UNION SELECT 'Bitel')
WHERE NOT EXISTS (SELECT 1 FROM operador);

INSERT INTO cuenta (titular, saldo)
SELECT 'Cliente Demo', 500.00
WHERE NOT EXISTS (SELECT 1 FROM cuenta);
```

**Inicialización automática:** al arrancar, el backend ejecuta este `schema.sql` contra el archivo `banco.db`. Como usa `CREATE TABLE IF NOT EXISTS` y siembra solo si las tablas están vacías, es seguro ejecutarlo en cada arranque: si la base ya existe con datos, no la toca; si el archivo no existe, lo crea y lo puebla. Así el profesor nunca corre scripts: solo `dotnet run`.

**Restricciones clave:** `CHECK (saldo >= 0)` y `CHECK (monto > 0)` son la última línea de defensa contra datos inconsistentes, incluso si fallara la validación de la aplicación. La columna `idempotency_key` UNIQUE garantiza que el doble clic no genere dos recargas.

> Nota sobre tipos en SQLite: `REAL` es de punto flotante. Para dinero es suficiente en esta práctica, pero si se quisiera exactitud decimal se puede almacenar en céntimos como `INTEGER`. Se deja en `REAL` por simplicidad y legibilidad para el cambio en vivo.

---

## 4. Contrato de la API

### `GET /api/operadores`
Devuelve el catálogo de operadores para poblar el desplegable del frontend.
Respuesta `200`: `[{ "id": 1, "nombre": "Claro" }, ...]`

### `POST /api/recargas`
Procesa una recarga (la Historia de Usuario completa).

**Request body:**
```json
{
  "celular": "987654321",
  "operadorId": 1,
  "monto": 20.00,
  "idempotencyKey": "guid-generado-en-frontend"
}
```

**Respuestas:**
| Código | Caso | Body |
|--------|------|------|
| `200` | Éxito | `{ "ok": true, "mensaje": "Recarga realizada con éxito", "saldoRestante": 480.00 }` |
| `400` | Celular inválido / monto inválido / operador inexistente / faltan campos | `{ "ok": false, "error": "..." }` |
| `409` | Saldo insuficiente | `{ "ok": false, "error": "Saldo insuficiente" }` |
| `200` (repetido) | Misma `idempotencyKey` | Devuelve el resultado de la primera vez, **sin volver a debitar** |

> Nota: como ya no hay login, no existe el caso 401 por clave. La confirmación de la recarga es directa.

---

## 5. Defensa contra las pruebas de estrés del docente

El profesor **no probará el camino feliz**. La arquitectura defiende en tres capas:

| Ataque del docente | Defensa | Dónde |
|--------------------|---------|-------|
| **Letras en campo numérico** (celular/monto) | Validación de tipo y formato: celular = `^[0-9]{9}$`, monto = decimal `> 0` y `<= límite`. Se rechaza con `400`. | `Validators/RecargaValidator.cs` (backend) + restricciones de input en `RecargaCelular.razor` (frontend) |
| **Inyección SQL** | **Consultas parametrizadas** con `Microsoft.Data.Sqlite` (`@param`). La entrada nunca se concatena en el SQL. | `Services/RecargaService.cs` |
| **Datos inconsistentes** (operador inexistente, monto negativo) | Validación contra catálogo + `CHECK` en la base + transacción que revierte si algo falla. | servicio + esquema SQL |
| **Doble clic rápido** | (1) Botón se deshabilita al enviar en el frontend; (2) `idempotency_key` UNIQUE en la base hace que la segunda inserción no debite y se devuelva el resultado original. | `RecargaCelular.razor` + tabla `recarga` |
| **Saldo insuficiente** | Verificación explícita antes de debitar; débito e inserción en **una sola transacción** (`BEGIN/COMMIT/ROLLBACK`). | `RecargaService.cs` |

> **Regla de oro:** validar en el frontend mejora la experiencia, pero **la validación que cuenta es la del backend**. El docente puede llamar la API directamente (Postman/curl) saltándose el frontend, así que toda regla de negocio vive en el backend.

---

## 6. Transacción del flujo de recarga (pseudocódigo)

Esto es lo que el agente debe implementar en `RecargaService.cs`. Conviene **memorizarlo y entenderlo** para el cambio en vivo del sábado:

```
ProcesarRecarga(datos):
    Validar(datos)                      # 400 si formato inválido
    using conn = AbrirConexion()
    using tx = conn.BeginTransaction()  # SQLite serializa la escritura (bloqueo de BD)
    try:
        operador = SELECT ... FROM operador WHERE id = @operadorId  # 400 si no existe
        cuenta   = SELECT saldo FROM cuenta WHERE id = @id
        si cuenta.saldo < datos.monto:  tx.Rollback() -> 409
        UPDATE cuenta SET saldo = saldo - @monto WHERE id = @id
        INSERT INTO recarga (..., idempotency_key) VALUES (...)
            # si la idempotency_key ya existe (violación UNIQUE):
            #   tx.Rollback(); devolver el resultado de la recarga previa SIN debitar
        tx.Commit()
        return { ok: true, mensaje: "Recarga realizada con éxito", saldoRestante }
    catch:
        tx.Rollback(); relanzar
```

**Concurrencia en SQLite:** no existe `SELECT ... FOR UPDATE` como en PostgreSQL, pero no hace falta: SQLite serializa las transacciones de escritura (bloquea la base durante la escritura), de modo que dos peticiones casi simultáneas se procesan una tras otra. La protección real contra el doble clic es la combinación de la **transacción** y la **`idempotency_key` UNIQUE**: la segunda inserción con la misma key viola la restricción, se hace rollback del débito duplicado y se devuelve el resultado original. Para el nivel de concurrencia de esta práctica (un evaluador) es más que suficiente.

---

## 7. Cómo se levanta (resumen — el detalle va en README.md)

**Backend:**
```bash
cd backend
dotnet run                  # API fija en http://localhost:5080
```

**Frontend:**
```bash
cd frontend
dotnet run                  # app Blazor fija en http://localhost:5081, ya cableada al backend
```

**Puertos fijos y cero configuración:** el backend siempre arranca en `5080` y el frontend en `5081` (definidos en `launchSettings.json`). El frontend ya conoce la URL del backend y el backend ya autoriza el origen del frontend por CORS, así que el profesor solo ejecuta `dotnet run` en cada carpeta y todo conecta sin tocar nada.

**Base de datos:** SQLite. El backend crea y puebla `banco.db` automáticamente al primer arranque (no requiere ejecutar scripts ni internet). El profesor solo hace `dotnet run`.

---

## 8. Por qué esta arquitectura es de "bajo costo de cambio"

El examen del sábado mide qué tan rápido implementas un cambio. Esta arquitectura ayuda porque:

- **Separación por capas:** un cambio de UI toca solo `RecargaCelular.razor`; una regla de negocio toca solo `RecargaService.cs`; una validación toca solo `RecargaValidator.cs`.
- **Sin ORM ni capas mágicas:** lo que ves es lo que corre. El SQL está a la vista y es modificable directamente.
- **Un solo lenguaje (C#) en ambos lados:** menos contexto que sostener bajo presión.
- **Validación centralizada:** agregar una regla nueva (p. ej. "monto máximo S/ 100") es editar un solo archivo.

---

## 9. Notas para el agente de IA

- Usa **.NET 10 (LTS, la versión LTS actual)**. Fija `<TargetFramework>net10.0</TargetFramework>` en ambos `.csproj`.
- **Por qué .NET 10 y no 8:** por defecto el runtime de .NET no salta versiones mayores, así que un proyecto necesita el runtime de SU misma versión mayor instalado en la máquina que lo ejecuta. No existe una versión "universal"; se elige la que es más probable que el profesor ya tenga. .NET 10 es la LTS actual y la que trae Visual Studio 2026, así que es la apuesta de mayor probabilidad.
- **Red de seguridad:** añade `<RollForward>Major</RollForward>` en ambos `.csproj`. Esto permite que el proyecto se ejecute con un runtime de una versión MAYOR a la 10 si el profesor tuviera una más nueva. (No cubre el caso de que tenga una más vieja; para eso el README indica qué runtime instalar.)
- No agregues paquetes fuera de lo necesario: backend solo `Microsoft.Data.Sqlite` (más lo que trae la plantilla Web API). Frontend, lo que trae Blazor WASM. **No uses Entity Framework Core.**
- Toda consulta SQL **debe** ser parametrizada con `Microsoft.Data.Sqlite`. Nunca concatenar entradas en el SQL.
- Configura **CORS** en el backend permitiendo explícitamente el origen del frontend (`http://localhost:5081`).
- Fija los puertos en `launchSettings.json` (backend `5080`, frontend `5081`), perfil HTTP sin HTTPS, para que `dotnet run` funcione sin que el profesor configure nada ni aparezca el prompt de certificado de desarrollo.
- En el arranque, ejecuta `schema.sql` contra `banco.db` (con `CREATE TABLE IF NOT EXISTS` y siembra condicional) para que la base se cree y pueble sola. Habilita `PRAGMA foreign_keys = ON;` al abrir cada conexión.
- Implementa idempotencia y transacción tal como el pseudocódigo de la sección 6.
- Implementa idempotencia y transacción tal como el pseudocódigo de la sección 6.
- El frontend debe deshabilitar el botón "Confirmar" mientras la petición está en curso.
- Incluye un `README.md` con el paso a paso exacto (incluido cómo instalar el .NET 10 SDK y qué hacer si falta el runtime de .NET 10), puertos y datos de prueba.
