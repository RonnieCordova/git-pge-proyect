using Microsoft.AspNetCore.Mvc;
using ef_core.Data;
using ef_core.DTOs;

[Route("api/[controller]")]
[ApiController]
public class SeatDataController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SeatDataController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> PostSeatData([FromBody] SeatDataDTO seatDataDto)
    {
        // 1. Convertir el DTO a la entidad de la base de datos
        var seatData = new SeatData
        {
            Area = seatDataDto.Area,
            Nombre = seatDataDto.Nombre,
            Apellido = seatDataDto.Apellido,
            HoraEntrada = seatDataDto.HoraEntrada,
            HoraSalidaAlmuerzo = seatDataDto.HoraSalidaAlmuerzo,
            HoraRegresoAlmuerzo = seatDataDto.HoraRegresoAlmuerzo,
            HoraSalida = seatDataDto.HoraSalida,
            Detalle = seatDataDto.Detalle,
            TipoPermiso = seatDataDto.TipoPermiso
        };

        // 2. Guardar la entidad en la base de datos
        _context.SeatData.Add(seatData);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(PostSeatData), new { id = seatData.Id }, seatData);
    }
}