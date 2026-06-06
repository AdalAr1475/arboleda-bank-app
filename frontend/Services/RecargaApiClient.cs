using System.Net.Http.Json;
using Frontend.Models;

namespace Frontend.Services;

/// <summary>Cliente tipado contra la Web API del backend (puerto fijo 5080).</summary>
public class RecargaApiClient
{
    private readonly HttpClient _http;

    public RecargaApiClient(HttpClient http) => _http = http;

    public async Task<List<Operador>> GetOperadoresAsync()
    {
        return await _http.GetFromJsonAsync<List<Operador>>("api/operadores")
               ?? new List<Operador>();
    }

    /// <summary>
    /// Envía la recarga. Devuelve el cuerpo de respuesta tanto en éxito (200) como en
    /// error de negocio (400/409), porque el backend siempre responde un RecargaResponse.
    /// </summary>
    public async Task<RecargaResponse> PostRecargaAsync(RecargaRequest request)
    {
        var http = await _http.PostAsJsonAsync("api/recargas", request);

        var body = await http.Content.ReadFromJsonAsync<RecargaResponse>();
        if (body is not null)
            return body;

        // Respuesta sin cuerpo legible (p. ej. 500 con ProblemDetails).
        return new RecargaResponse
        {
            Ok = false,
            Error = $"Error del servidor ({(int)http.StatusCode})"
        };
    }
}
