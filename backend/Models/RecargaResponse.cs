namespace Backend.Models;

/// <summary>Respuesta de POST /api/recargas.</summary>
public class RecargaResponse
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
    public decimal? SaldoRestante { get; set; }
    public string? Error { get; set; }
}
