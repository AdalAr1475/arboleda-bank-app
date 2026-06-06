namespace Backend.Models;

/// <summary>Cuerpo de la petición POST /api/recargas.</summary>
public class RecargaRequest
{
    public string Celular { get; set; } = "";
    public int OperadorId { get; set; }
    public decimal Monto { get; set; }
    public string IdempotencyKey { get; set; } = "";
}
