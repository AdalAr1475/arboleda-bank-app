# Banco Arboleda — CU-01 Recarga Celular (PC2)

Proyecto de la práctica del curso **SI806 — Desarrollo Adaptativo**. Implementa
**únicamente** la Historia de Usuario **Recarga Celular** del Banco Arboleda: el cliente
elige número, operador y monto, y el sistema debita el saldo y registra la recarga.

No hay login, ni Pago de Servicio, ni Transferencia (fuera de alcance).

**Stack:** C# / .NET 10.
- `backend/` — ASP.NET Core Web API (REST/JSON).
- `frontend/` — Blazor WebAssembly.
- Base de datos — **SQLite embebido** (archivo `banco.db`), sin servidor ni internet.

---

## 1. Requisitos previos

- **.NET 10 SDK.** Descárgalo de <https://dotnet.microsoft.com/download>.
  Verifica la instalación con:
  ```bash
  dotnet --version
  ```
  Debe mostrar `10.x` (p. ej. `10.0.300`).

- **No se requiere nada más:** sin internet, sin instalar PostgreSQL / SQL Server, sin
  Docker, sin ninguna herramienta de base de datos. SQLite va embebido en el backend.

> ### ⚠️ Si `dotnet run` falla con un error tipo:
> ```
> You must install or update .NET to run this application.
> Framework: 'Microsoft.NETCore.App', version '10.0.x' not found
> ```
> Significa que tu máquina **no tiene el runtime de .NET 10** (este es el punto de fallo
> más probable). Solución: instala el **ASP.NET Core Runtime 10** (o directamente el
> **.NET 10 SDK**) desde <https://dotnet.microsoft.com/download>. Puede convivir con otras
> versiones de .NET ya instaladas. Ambos `.csproj` incluyen `<RollForward>Major</RollForward>`,
> así que también funcionarán con un runtime de versión **mayor** a la 10 si tuvieras una
> más nueva.

---

## 2. Base de datos (SQLite, automática)

- La base es un único archivo SQLite, **`backend/bin/.../banco.db`**, que el backend
  **crea y puebla solo en el primer arranque** (ejecuta `schema.sql` con
  `CREATE TABLE IF NOT EXISTS` y siembra condicional).
- **No instalas nada, no ejecutas scripts y no necesitas internet** para la base de datos.
- Si quieres empezar de cero, basta con borrar el archivo `banco.db`: se regenerará en el
  siguiente arranque.

---

## 3. Levantar el BACKEND

En una terminal:

```bash
cd backend
dotnet run
```

El backend queda escuchando en **http://localhost:5080**. No requiere configurar nada.
(La primera vez `dotnet run` restaura paquetes; si tu máquina no tiene internet para NuGet,
ejecuta antes `dotnet restore` con conexión, una sola vez.)

---

## 4. Levantar el FRONTEND

En **otra** terminal (con el backend ya corriendo):

```bash
cd frontend
dotnet run
```

El frontend queda en **http://localhost:5081** y abre el navegador automáticamente.
Ya viene **cableado** para hablar con el backend en `5080`. No requiere configurar nada.

---

## 5. Puertos usados (fijos)

| Componente | URL                     |
|------------|-------------------------|
| Backend    | http://localhost:5080   |
| Frontend   | http://localhost:5081   |

Vienen cableados entre sí: el frontend conoce la URL de la API
(`frontend/wwwroot/appsettings.json`) y el backend autoriza por **CORS** el origen del
frontend. **No necesitas cambiar ni asociar nada.**

> **Caso excepcional:** si alguno de esos puertos ya está ocupado en tu máquina, es el
> **único** lugar a tocar: `applicationUrl` en `backend/Properties/launchSettings.json` y/o
> `frontend/Properties/launchSettings.json` (y, si cambias el del backend, también
> `ApiBaseUrl` en `frontend/wwwroot/appsettings.json` y el origen permitido por CORS en
> `backend/Program.cs`).

---

## 6. Datos de prueba

- **Cuenta demo:** "Cliente Demo" con saldo inicial **S/ 500.00**.
- **Operadores disponibles:** Claro, Movistar, Entel, Bitel.

---

## 7. Cómo probar el flujo completo

1. Arranca el **backend** (`cd backend && dotnet run`) y espera a que diga que escucha en
   `http://localhost:5080`.
2. Arranca el **frontend** (`cd frontend && dotnet run`); se abrirá el navegador en
   `http://localhost:5081`.
3. En la pantalla de recarga: escribe un **número de 9 dígitos** (p. ej. `987654321`),
   elige un **operador** y un **monto** (p. ej. S/ 20).
4. Pulsa **Confirmar recarga**. Verás **"Recarga realizada con éxito"** y el **nuevo saldo**
   (p. ej. S/ 480.00).
5. Pulsa **Hacer otra recarga** para repetir el flujo.

(Opcional — probar la API directa con Postman/curl, sin frontend:)
```bash
curl -X POST http://localhost:5080/api/recargas \
  -H "Content-Type: application/json" \
  -d "{\"celular\":\"987654321\",\"operadorId\":1,\"monto\":20,\"idempotencyKey\":\"demo-1\"}"
```

---

## 8. Orden de arranque

1. **Primero el backend** (crea y puebla la base al arrancar).
2. **Luego el frontend.**

Si abres el frontend sin el backend corriendo, la lista de operadores no cargará y la
recarga mostrará un error de conexión; arranca el backend y recarga la página.
