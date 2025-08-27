using ef_core.Data;
using ef_core.DTOs;

namespace ef_core.Services;
public class BiometricoDataService
{
    private readonly ApplicationDbContext _context;

    public BiometricoDataService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddBiometricoDataAsync(BiometricoDataDTO dto)
    {
        // Convertir DTO a la entidad de la base de datos
        var biometricoData = new BiometricoData
        {
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Hora = dto.Hora,
            Detalle = dto.Detalle,
            EsEntrada = dto.EsEntrada,
            EsSalida = dto.EsSalida,
            EsSalidaAlmuerzo = dto.EsSalidaAlmuerzo,
            EsLlegadaAlmuerzo = dto.EsLlegadaAlmuerzo
        };

        _context.BiometricoData.Add(biometricoData);
        await _context.SaveChangesAsync();
    }
}