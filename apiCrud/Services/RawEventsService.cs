using ef_core.Data;
using ef_core.DTOs;

namespace ef_core.Services
{
    public class RawEventService
    {
        private readonly ApplicationDbContext _context;

        public RawEventService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddRawEvent(RawEventDTO dto)
        {
            var rawEvent = new RawEvent
            {
                UsuarioId = dto.UsuarioId,
                DispositivoId = dto.DispositivoId,
                TipoEvento = dto.TipoEvento,
                MarcaDeTiempo = dto.MarcaDeTiempo,
                Payload_json = dto.Payload_json,
                Lote_ingesta = dto.Lote_ingesta
            };

            _context.RawEvents.Add(rawEvent);
            await _context.SaveChangesAsync();
        }
    }
}