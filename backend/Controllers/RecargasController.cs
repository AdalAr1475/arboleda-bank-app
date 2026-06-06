using Backend.Models;
using Backend.Services;
using Backend.Validators;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api")]
public class RecargasController : ControllerBase
{
    private readonly RecargaService _service;
    private readonly ILogger<RecargasController> _logger;

    public RecargasController(RecargaService service, ILogger<RecargasController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Catálogo de operadores para poblar el desplegable del frontend.</summary>
    [HttpGet("operadores")]
    public ActionResult<IEnumerable<OperadorDto>> GetOperadores()
    {
        return Ok(_service.ObtenerOperadores());
    }

    /// <summary>Procesa una recarga (la Historia de Usuario completa).</summary>
    [HttpPost("recargas")]
    public ActionResult<RecargaResponse> PostRecarga([FromBody] RecargaRequest request)
    {
        // 1) Validación de entrada (la que cuenta vive en el backend).
        var errores = RecargaValidator.Validar(request);
        if (errores.Count > 0)
            return BadRequest(new RecargaResponse { Ok = false, Error = string.Join(". ", errores) });

        // 2) Reglas de negocio + transacción idempotente.
        try
        {
            var result = _service.ProcesarRecarga(request);
            return result.Status switch
            {
                RecargaStatus.Ok => Ok(result.Response),
                RecargaStatus.Conflict => Conflict(result.Response),
                _ => BadRequest(result.Response),
            };
        }
        catch (Exception ex)
        {
            // Nunca filtramos el stack al cliente.
            _logger.LogError(ex, "Error procesando recarga");
            return StatusCode(500, new RecargaResponse
            {
                Ok = false,
                Error = "Ocurrió un error al procesar la recarga"
            });
        }
    }
}
