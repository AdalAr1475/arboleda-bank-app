using Microsoft.Data.Sqlite;

namespace Backend.Data;

/// <summary>
/// Fábrica de conexiones SQLite. Resuelve la ruta del archivo de forma robusta
/// (relativa al directorio del ejecutable) y activa las claves foráneas en cada
/// conexión. También inicializa el esquema ejecutando schema.sql al arrancar.
/// </summary>
public class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration config)
    {
        var raw = config.GetConnectionString("Default") ?? "Data Source=banco.db";

        // Resolver "Data Source=archivo" a una ruta absoluta junto al ejecutable,
        // para que funcione sin importar desde dónde se ejecute `dotnet run`.
        var builder = new SqliteConnectionStringBuilder(raw);
        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        }
        _connectionString = builder.ToString();
    }

    /// <summary>Abre una conexión y activa PRAGMA foreign_keys = ON.</summary>
    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    /// <summary>
    /// Ejecuta schema.sql contra la base. Idempotente: usa CREATE TABLE IF NOT EXISTS
    /// y siembra condicional, así que es seguro correrlo en cada arranque.
    /// </summary>
    public void InitSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
        var sql = File.ReadAllText(schemaPath);

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
