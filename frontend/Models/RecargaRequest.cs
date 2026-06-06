namespace Frontend.Models;

public class RecargaRequest
{
    public string Celular { get; set; } = "";
    public int OperadorId { get; set; }
    public decimal Monto { get; set; }
    public string IdempotencyKey { get; set; } = "";
}
