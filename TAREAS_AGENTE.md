# TAREAS_AGENTE.md — Construcción del repositorio PC2 Banco Arboleda

> Plan de trabajo para el agente de IA. Léelo completo antes de empezar.
> El **qué** y el **porqué** de la arquitectura están en `ARQUITECTURA.md` (ya en la raíz). Este documento es el **cómo paso a paso** y el **checklist de verificación**.
> Caso: **CU-03 Recarga celular** del Banco Arboleda (curso SI806 — Desarrollo Adaptativo).
> Stack: **C# / .NET 10**. Backend = ASP.NET Core Web API. Frontend = Blazor WebAssembly.
> Alcance: **SOLO la Historia de Usuario de Recarga Celular.** Sin login, sin Pago de Servicio, sin Transferencia.

---

## 0. Reglas que NO se negocian

1. **Dos subcarpetas en la raíz:** `backend/` (Web API) y `frontend/` (Blazor WASM). Nada de mezclarlas.
2. **Todo en C# / .NET 10 (LTS actual).** Fija `<TargetFramework>net10.0</TargetFramework>` en `backend/Backend.csproj` y `frontend/Frontend.csproj`, y añade `<RollForward>Major</RollForward>` en ambos. Motivo: el runtime de .NET no salta versiones mayores por defecto, así que el ejecutor necesita el runtime de la misma versión mayor; .NET 10 es la LTS actual (la de Visual Studio 2026) y la que el profesor muy probablemente tiene. `RollForward=Major` añade tolerancia hacia versiones aún más nuevas. El README debe indicar claramente qué runtime instalar por si su versión fuese anterior.
3. **Dependencias mínimas.** Backend: solo `Microsoft.Data.Sqlite` además de la plantilla Web API. Frontend: solo lo que trae Blazor WASM. **Prohibido Entity Framework Core** y cualquier ORM.
4. **Toda consulta SQL parametrizada** con `Microsoft.Data.Sqlite` (`@param`). Prohibido concatenar entradas del usuario en el SQL.
5. **La validación que cuenta es la del backend.** El profesor puede llamar la API con Postman/curl saltándose el frontend.
6. **El proyecto debe levantarse con `dotnet run`** en cada carpeta (precedido de `dotnet restore`).
   - **CERO configuración del profesor.** Él solo entra a cada carpeta y ejecuta `dotnet run`. No asigna puertos, no edita archivos, no asocia nada.
   - **Puertos FIJOS, decididos por nosotros y escritos en el repo:** backend en `http://localhost:5080`, frontend en `http://localhost:5081`. Se fijan en `backend/Properties/launchSettings.json` y `frontend/Properties/launchSettings.json` con `applicationUrl` explícito. NO usar puertos aleatorios ni HTTPS con certificado de desarrollo (evita el prompt de `dotnet dev-certs`).
   - El frontend ya trae cableada la URL del backend (`http://localhost:5080`) y el backend ya autoriza por CORS el origen del frontend (`http://localhost:5081`). Todo cuadra solo, sin que el profesor toque nada.
7. **El README es obligatorio** y debe llevar de "máquina sin nada instalado" a "proyecto corriendo" (ver sección 5).
8. **Base de datos: SQLite (archivo local en el repo).** Sin servidor, sin nube, sin internet, sin contraseñas. El backend crea y puebla `banco.db` automáticamente al arrancar (ver tareas de backend). El profesor NO instala nada ni ejecuta scripts: solo `dotnet run`.
9. **Alcance estricto:** solo la recarga de celular. No generes login, ni pantallas de Pago/Transferencia, ni tablas o endpoints para ellos.

---

## 1. Estructura objetivo

```
banco-arboleda-pc2/
├── README.md                 # GENERAR (entregable, ver sección 5)
├── ARQUITECTURA.md           # ya existe, no tocar
├── TAREAS_AGENTE.md          # este archivo
├── backend/                  # ASP.NET Core Web API (.NET 10)
│   ├── Backend.csproj
│   ├── Program.cs
│   ├── appsettings.json      # ruta del archivo SQLite
│   ├── schema.sql            # DDL + semilla, ejecutado en el arranque
│   ├── banco.db              # se genera solo al primer arranque (no es obligatorio versionarlo)
│   ├── .gitignore            # bin/, obj/
│   ├── Controllers/RecargasController.cs
│   ├── Services/RecargaService.cs
│   ├── Data/Db.cs
│   ├── Models/RecargaRequest.cs
│   ├── Models/RecargaResponse.cs
│   └── Validators/RecargaValidator.cs
└── frontend/                 # Blazor WebAssembly (.NET 10)
    ├── Frontend.csproj
    ├── Program.cs
    ├── .gitignore
    ├── wwwroot/appsettings.json
    ├── Pages/RecargaCelular.razor
    ├── Services/RecargaApiClient.cs
    ├── Models/RecargaRequest.cs
    └── Models/RecargaResponse.cs
```

---

## 2. Tareas del BACKEND (ASP.NET Core Web API)

- [ ] Crea el proyecto: `dotnet new webapi -n Backend` dentro de `backend/` (o estructura equivalente). En `Backend.csproj` asegúrate de `<TargetFramework>net10.0</TargetFramework>` y agrega `<RollForward>Major</RollForward>`.
- [ ] Agrega el paquete: `dotnet add package Microsoft.Data.Sqlite`.
- [ ] **Cadena de conexión:** simple, apuntando a un archivo local junto al ejecutable, en `appsettings.json`:
  ```
  "ConnectionStrings": {
    "Default": "Data Source=banco.db"
  }
  ```
  No hay credenciales ni host. Resuelve la ruta de forma robusta para que funcione sin importar desde dónde se ejecute `dotnet run`.
- [ ] `Data/Db.cs`: fábrica que crea y abre una `SqliteConnection` usando la cadena de configuración. **Ejecuta `PRAGMA foreign_keys = ON;`** tras abrir cada conexión (SQLite no aplica claves foráneas por defecto).
- [ ] **Inicialización automática:** al arrancar (en `Program.cs` o en `Db.cs`), ejecuta `schema.sql` contra la base. Con `CREATE TABLE IF NOT EXISTS` y siembra condicional es seguro correrlo en cada arranque: crea y puebla si no existe, no toca nada si ya hay datos.
- [ ] `Program.cs`: registra servicios, **configura CORS** permitiendo explícitamente el origen del frontend (`http://localhost:5081`), mapea controladores y arranca. No uses `AllowAnyOrigin` junto a credenciales; aquí basta permitir ese origen concreto.
- [ ] `Properties/launchSettings.json`: fija `applicationUrl` en `http://localhost:5080`, perfil HTTP (no HTTPS), `launchBrowser: false`. Así `dotnet run` siempre abre el mismo puerto sin pedir certificado.
- [ ] `Models/RecargaRequest.cs`: `Celular` (string), `OperadorId` (int), `Monto` (decimal), `IdempotencyKey` (string).
- [ ] `Models/RecargaResponse.cs`: `Ok` (bool), `Mensaje` (string), `SaldoRestante` (decimal?), `Error` (string?).
- [ ] `Validators/RecargaValidator.cs`: valida el request y devuelve lista de errores. Reglas:
  - `Celular`: cumple `^[0-9]{9}$` (exactamente 9 dígitos, sin letras ni espacios).
  - `Monto`: decimal `> 0` y `<= 500`. Rechaza no numérico, cero y negativo.
  - `OperadorId`: entero positivo que exista en `operador`.
  - `IdempotencyKey`: presente y no vacío.
- [ ] `Services/RecargaService.cs`: implementa la transacción EXACTAMENTE como el pseudocódigo de la sección 6 de `ARQUITECTURA.md`:
  - `BeginTransaction` → valida operador (400) → lee saldo de la cuenta → verifica saldo (409) → `UPDATE saldo` → `INSERT recarga` con `idempotency_key`. (SQLite no tiene `FOR UPDATE`; la transacción + `idempotency_key` UNIQUE bastan, ver sección 6 de ARQUITECTURA.md.)
  - Maneja violación de UNIQUE en `idempotency_key`: NO debita de nuevo; devuelve el resultado de la primera recarga.
  - `Rollback` ante cualquier error.
  - Todas las consultas con parámetros de `Microsoft.Data.Sqlite` (`@id`, `@monto`, etc.).
- [ ] `Controllers/RecargasController.cs`:
  - `GET /api/operadores` → lista de operadores.
  - `POST /api/recargas` → orquesta validador → servicio y traduce a 200/400/409. Captura errores; nunca filtra el stack al cliente.
- [ ] `schema.sql`: el DDL y los datos semilla de la sección 3 de `ARQUITECTURA.md` (sintaxis SQLite, con `CREATE TABLE IF NOT EXISTS` y siembra condicional). Lo ejecuta el backend en el arranque; el profesor nunca lo corre a mano.
- [ ] `.gitignore`: `bin/`, `obj/`. Opcionalmente ignora `banco.db` (se regenera solo); NO ignores `appsettings.json` ni `schema.sql`.

---

## 3. Tareas del FRONTEND (Blazor WebAssembly)

- [ ] Crea el proyecto: `dotnet new blazorwasm -n Frontend` dentro de `frontend/`. En `Frontend.csproj` asegúrate de `<TargetFramework>net10.0</TargetFramework>` y agrega `<RollForward>Major</RollForward>`.
- [ ] `wwwroot/appsettings.json` con la URL base de la API ya cableada al puerto fijo del backend: `{ "ApiBaseUrl": "http://localhost:5080" }`. No es necesario que el profesor lo edite.
- [ ] `Properties/launchSettings.json`: fija `applicationUrl` en `http://localhost:5081`, perfil HTTP (no HTTPS), `launchBrowser: true` (que abra solo el navegador en la app de recarga).
- [ ] `Models/`: réplicas de `RecargaRequest` y `RecargaResponse` para deserializar.
- [ ] `Services/RecargaApiClient.cs`: `HttpClient` tipado con métodos `GetOperadoresAsync()` y `PostRecargaAsync(request)`.
- [ ] `Pages/RecargaCelular.razor`: la Historia de Usuario completa en una sola pantalla. Implementa el flujo del caso:
  1. Campo número de celular — restringe a dígitos, máximo 9.
  2. Desplegable de operador — poblado desde `GET /api/operadores`.
  3. Campo/selección de monto — solo numérico, `> 0`.
  4. Botón **Confirmar recarga** → al éxito, muestra "Recarga realizada con éxito" y el saldo restante.
- [ ] **Defensa contra doble clic:** genera un `IdempotencyKey` (`Guid.NewGuid()`) al cargar el formulario; deshabilita el botón mientras la petición está en curso; renueva la key solo tras una recarga exitosa.
- [ ] Muestra mensajes de error claros para 400/409 que devuelva el backend.
- [ ] Validación en el cliente que refleje (no reemplace) la del backend.
- [ ] (Opcional, recomendado) aplica el diseño aprobado del prototipo de la IA de diseño.

> Nota: NO hay página de login ni menú principal con otras opciones. La app abre directamente (o con un paso mínimo) en la pantalla de recarga.

---

## 4. Pruebas que el agente debe pasar antes de declarar "terminado"

El profesor intentará romper la app. Verifica cada caso (UI + Postman/curl directo a la API):

- [ ] **Camino feliz:** recarga válida → 200 + "Recarga realizada con éxito" + saldo debitado correctamente.
- [ ] **Letras en celular** (`98abc4321`) → 400, sin tocar la base.
- [ ] **Celular con ≠ 9 dígitos** (`12345`) → 400.
- [ ] **Monto no numérico / negativo / cero** → 400.
- [ ] **Operador inexistente** (`operadorId` que no está) → 400.
- [ ] **Inyección SQL** en celular (`'; DROP TABLE recarga;--`) → 400 o tratado como literal; **la tabla sigue intacta**.
- [ ] **Saldo insuficiente** (monto > saldo) → 409, sin débito.
- [ ] **Doble clic / misma IdempotencyKey dos veces** → una sola recarga, un solo débito.
- [ ] **Llamada directa a la API sin frontend** (Postman) → todas las validaciones siguen aplicando.
- [ ] El proyecto se levanta desde cero siguiendo solo el README, sin pasos no documentados.

---

## 5. README.md (ENTREGABLE — el agente lo genera)

Este README lo lee el PROFESOR. Debe llevar de "máquina sin nada" a "proyecto corriendo". Incluye, en este orden:

1. **Descripción breve** del proyecto y del caso (Recarga celular — Banco Arboleda).
2. **Requisitos previos:**
   - **.NET 10 SDK** — enlace a https://dotnet.microsoft.com/download e indicación de verificar con `dotnet --version` (debe mostrar `10.x`).
   - **Si `dotnet run` falla con un error tipo "framework 'Microsoft.NETCore.App' 10.0.x no encontrado":** la máquina no tiene el runtime de .NET 10. Solución: instalar el **ASP.NET Core Runtime 10** (o el SDK 10) desde el enlace anterior; puede convivir con otras versiones. Indicar esto de forma destacada porque es el punto de fallo más probable.
   - No se requiere internet, ni instalar PostgreSQL/SQL Server, ni Docker, ni ninguna herramienta de base de datos (SQLite va embebido).
3. **Base de datos (SQLite, automática):**
   - La base es un archivo SQLite (`banco.db`) que el backend crea y puebla solo al primer arranque.
   - El profesor no instala nada, no ejecuta scripts y no necesita internet para la base.
4. **Levantar el backend:**
   ```bash
   cd backend
   dotnet run
   ```
   El backend queda en `http://localhost:5080`. No requiere configurar nada.
5. **Levantar el frontend** (en otra terminal, con el backend ya corriendo):
   ```bash
   cd frontend
   dotnet run
   ```
   El frontend queda en `http://localhost:5081` y ya está cableado para hablar con el backend en `5080`. No requiere configurar nada.
6. **Puertos usados (fijos):** backend `5080`, frontend `5081`. Ya vienen cableados entre sí (URL de API y CORS); el profesor no necesita cambiar ni asociar nada. Si algún puerto estuviera ocupado en su máquina, indicar el único lugar a tocar (`launchSettings.json`) como caso excepcional.
7. **Datos de prueba:** cuenta demo con saldo inicial `500.00`, operadores disponibles (Claro, Movistar, Entel, Bitel).
8. **Cómo probar el flujo completo** en 4–5 pasos.
9. **Orden de arranque:** primero el backend (crea/puebla la base al arrancar), luego el frontend.

> El README debe ser autosuficiente: si el profesor sigue solo este archivo, la app debe quedar corriendo sin adivinar nada.

---

## 6. Entrega final

- [ ] Verifica que existan SOLO las dos subcarpetas `backend/` y `frontend/`.
- [ ] Verifica que `bin/`, `obj/` NO se incluyan, pero que `appsettings.json` y `schema.sql` SÍ estén presentes.
- [ ] Verifica que toda la sección 4 (pruebas) pasa.
- [ ] **Verifica el target framework:** ambos `.csproj` dicen `net10.0` y llevan `<RollForward>Major</RollForward>`; `dotnet build` compila sin error.
- [ ] Confirma que el README permite levantar todo desde cero con el .NET 10 SDK (sin internet ni base de datos externa) e incluye la nota de qué hacer si falta el runtime de .NET 10.
