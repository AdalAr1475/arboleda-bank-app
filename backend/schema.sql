-- Esquema SQLite del CU-01 Recarga Celular (Banco Arboleda).
-- Seguro de ejecutar en cada arranque: CREATE TABLE IF NOT EXISTS + siembra condicional.

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
