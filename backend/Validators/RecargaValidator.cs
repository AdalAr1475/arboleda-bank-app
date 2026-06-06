using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Validators;

/// <summary>
/// Validación de entrada centralizada (capa de aplicación). Es la primera línea
/// de defensa; las restricciones CHECK de la base son la última.
/// </summary>
public static class RecargaValidator
{
    // Monto máximo aceptado por esta práctica (= saldo inicial de la cuenta demo).
    public const decimal MontoMaximo = 500m;

    private static readonly Regex CelularRegex = new(@"^[0-9]{9}$", RegexOptions.Compiled);

    /// <summary>Devuelve la lista de errores; vacía si el request es válido.</summary>
    public static List<string> Validar(RecargaRequest req)
    {
        var errores = new List<string>();

        if (req is null)
        {
            errores.Add("Petición vacía");
            return errores;
        }

        // Celular: exactamente 9 dígitos, sin letras ni espacios.
        if (string.IsNullOrWhiteSpace(req.Celular) || !CelularRegex.IsMatch(req.Celular))
            errores.Add("El número de celular debe tener exactamente 9 dígitos");

        // Monto: numérico > 0 y <= límite. (El no-numérico se rechaza al deserializar.)
        if (req.Monto <= 0)
            errores.Add("El monto debe ser mayor a 0");
        else if (req.Monto > MontoMaximo)
            errores.Add($"El monto no puede superar S/ {MontoMaximo:0.00}");

        // OperadorId: entero positivo (la existencia real se comprueba contra el catálogo).
        if (req.OperadorId <= 0)
            errores.Add("Operador inválido");

        // IdempotencyKey: presente y no vacío.
        if (string.IsNullOrWhiteSpace(req.IdempotencyKey))
            errores.Add("Falta la clave de idempotencia");

        return errores;
    }
}
