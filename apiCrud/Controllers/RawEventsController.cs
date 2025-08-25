using ef_core.DTOs;
using ef_core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ef_core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RawEventsController : ControllerBase
    {
        private readonly RawEventService _rawEventService;

        public RawEventsController(RawEventService rawEventService)
        {
            _rawEventService = rawEventService;
        }

        [HttpPost]
        public async Task<IActionResult> AddRawEvent([FromBody] RawEventDTO rawEventDto)
        {
            await _rawEventService.AddRawEvent(rawEventDto);
            return Ok();
        }
    }
}