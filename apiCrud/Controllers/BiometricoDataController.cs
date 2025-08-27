using Microsoft.AspNetCore.Mvc;
using ef_core.Data;
using ef_core.DTOs;

[Route("api/[controller]")]
[ApiController]
public class BiometricoDataController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BiometricoDataController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> PostBiometricoData([FromBody] BiometricoDataDTO biometricoDataDto)
    {
        // 1. Convertir el DTO a la entidad de la base de datos
        var biometricoData = new BiometricoData
        {
            Nombre = biometricoDataDto.Nombre,
            Apellido = biometricoDataDto.Apellido,
            Hora = biometricoDataDto.Hora,
            Detalle = biometricoDataDto.Detalle,
            EsEntrada = biometricoDataDto.EsEntrada,
            EsSalida = biometricoDataDto.EsSalida,
            EsSalidaAlmuerzo = biometricoDataDto.EsSalidaAlmuerzo,
            EsLlegadaAlmuerzo = biometricoDataDto.EsLlegadaAlmuerzo
        };

        // 2. Guardar la entidad en la base de datos
        _context.BiometricoData.Add(biometricoData);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(PostBiometricoData), new { id = biometricoData.Id }, biometricoData);
    }
}