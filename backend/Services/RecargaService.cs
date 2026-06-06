using Backend.Data;
using Backend.Models;
using Microsoft.Data.Sqlite;

namespace Backend.Services;

public enum RecargaStatus { Ok, BadRequest, Conflict }

/// <summary>Resultado del servicio, con el estado HTTP que le corresponde.</summary>
public record RecargaResult(RecargaStatus Status, RecargaResponse Response);

/// <summary>
/// Reglas de negocio de la recarga: débito + registro en UNA sola transacción,
/// con idempotencia. Implementa el pseudocódigo de la sección 6 de ARQUITECTURA.md.
/// Todas las consultas usan parámetros (@param): la entrada nunca se concatena en el SQL.
/// </summary>
public class RecargaService
{
    // SQLite: violación de restricción UNIQUE.
    private const int SQLITE_CONSTRAINT = 19;

    private readonly Db _db;

    public RecargaService(Db db) => _db = db;

    public RecargaResult ProcesarRecarga(RecargaRequest datos)
    {
        using var conn = _db.OpenConnection();

        // Idempotencia (caso común: doble envío no simultáneo). Si la key ya se usó,
        // devolvemos el resultado original con el saldo actual, SIN volver a debitar.
        var previo = BuscarRecargaPrevia(conn, datos.IdempotencyKey);
        if (previo is not null)
            return Exito(previo.Value.saldoActual);

        using var tx = conn.BeginTransaction();
        try
        {
            // 1) Operador debe existir en el catálogo controlado (400 si no).
            if (!OperadorExiste(conn, tx, datos.OperadorId))
            {
                tx.Rollback();
                return new RecargaResult(RecargaStatus.BadRequest,
                    new RecargaResponse { Ok = false, Error = "Operador inexistente" });
            }

            // 2) Leer la cuenta (una sola, precargada; no hay login).
            var cuenta = LeerCuenta(conn, tx);
            if (cuenta is null)
            {
                tx.Rollback();
                return new RecargaResult(RecargaStatus.BadRequest,
                    new RecargaResponse { Ok = false, Error = "No existe una cuenta configurada" });
            }
            var (cuentaId, saldo) = cuenta.Value;

            // 3) Verificar saldo suficiente ANTES de debitar (409 si no alcanza).
            if (saldo < datos.Monto)
            {
                tx.Rollback();
                return new RecargaResult(RecargaStatus.Conflict,
                    new RecargaResponse { Ok = false, Error = "Saldo insuficiente" });
            }

            // 4) Debitar el saldo.
            using (var upd = conn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE cuenta SET saldo = saldo - @monto WHERE id = @id;";
                upd.Parameters.AddWithValue("@monto", (double)datos.Monto);
                upd.Parameters.AddWithValue("@id", cuentaId);
                upd.ExecuteNonQuery();
            }

            // 5) Registrar la recarga. La idempotency_key UNIQUE protege del doble clic:
            //    una segunda inserción con la misma key lanza SQLITE_CONSTRAINT.
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
                    INSERT INTO recarga (cuenta_id, celular, operador_id, monto, idempotency_key)
                    VALUES (@cuentaId, @celular, @operadorId, @monto, @key);";
                ins.Parameters.AddWithValue("@cuentaId", cuentaId);
                ins.Parameters.AddWithValue("@celular", datos.Celular);
                ins.Parameters.AddWithValue("@operadorId", datos.OperadorId);
                ins.Parameters.AddWithValue("@monto", (double)datos.Monto);
                ins.Parameters.AddWithValue("@key", datos.IdempotencyKey);
                ins.ExecuteNonQuery();
            }

            var saldoRestante = saldo - datos.Monto;
            tx.Commit();
            return Exito(saldoRestante);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SQLITE_CONSTRAINT
                                          && ex.Message.Contains("idempotency_key"))
        {
            // Carrera real de doble clic: la 2.ª inserción violó la UNIQUE.
            // Revertimos el débito duplicado y devolvemos el resultado original.
            tx.Rollback();
            var previa = BuscarRecargaPrevia(conn, datos.IdempotencyKey);
            return Exito(previa?.saldoActual ?? LeerSaldoActual(conn));
        }
        catch
        {
            tx.Rollback();
            throw; // el controller responde 500 sin filtrar el stack al cliente
        }
    }

    public List<OperadorDto> ObtenerOperadores()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nombre FROM operador ORDER BY id;";
        var lista = new List<OperadorDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            lista.Add(new OperadorDto(reader.GetInt32(0), reader.GetString(1)));
        return lista;
    }

    // ----- helpers -----

    private static RecargaResult Exito(decimal saldoRestante) =>
        new(RecargaStatus.Ok, new RecargaResponse
        {
            Ok = true,
            Mensaje = "Recarga realizada con éxito",
            SaldoRestante = saldoRestante
        });

    private static bool OperadorExiste(SqliteConnection conn, SqliteTransaction tx, int operadorId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM operador WHERE id = @operadorId;";
        cmd.Parameters.AddWithValue("@operadorId", operadorId);
        return cmd.ExecuteScalar() is not null;
    }

    private static (int cuentaId, decimal saldo)? LeerCuenta(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, saldo FROM cuenta ORDER BY id LIMIT 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetInt32(0), Convert.ToDecimal(reader.GetDouble(1)));
    }

    private static decimal LeerSaldoActual(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT saldo FROM cuenta ORDER BY id LIMIT 1;";
        var v = cmd.ExecuteScalar();
        return v is null ? 0m : Convert.ToDecimal(v);
    }

    /// <summary>Busca una recarga ya registrada con esa idempotency_key.</summary>
    private static (long id, decimal saldoActual)? BuscarRecargaPrevia(SqliteConnection conn, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM recarga WHERE idempotency_key = @key LIMIT 1;";
        cmd.Parameters.AddWithValue("@key", key);
        var id = cmd.ExecuteScalar();
        if (id is null) return null;
        return (Convert.ToInt64(id), LeerSaldoActual(conn));
    }
}

public record OperadorDto(int Id, string Nombre);
