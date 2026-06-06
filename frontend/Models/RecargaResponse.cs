namespace Frontend.Models;

public class RecargaResponse
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
    public decimal? SaldoRestante { get; set; }
    public string? Error { get; set; }
}
