using ef_core.Data;
using ef_core.DTOs;

namespace ef_core.Services;
public class SeatDataService
{
    private readonly ApplicationDbContext _context;

    public SeatDataService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddSeatDataAsync(SeatDataDTO dto)
    {
        // Convertir DTO a la entidad de la base de datos
        var seatData = new SeatData
        {
            Area = dto.Area,
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            HoraEntrada = dto.HoraEntrada,
            HoraSalidaAlmuerzo = dto.HoraSalidaAlmuerzo,
            HoraRegresoAlmuerzo = dto.HoraRegresoAlmuerzo,
            HoraSalida = dto.HoraSalida,
            Detalle = dto.Detalle
        };

        _context.SeatData.Add(seatData);
        await _context.SaveChangesAsync();
    }
}