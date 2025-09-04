using ef_core.Services; // O tu namespace de servicios
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly UnificationService _unificationService;
    private readonly ExcelExportService _excelExportService;

    // Usamos inyección de dependencias para recibir los servicios que necesitamos
    public ReportesController(UnificationService unificationService, ExcelExportService excelExportService)
    {
        _unificationService = unificationService;
        _excelExportService = excelExportService;
    }

    [HttpGet("exportar-asistencia")]
    public async Task<IActionResult> ExportarAsistencia([FromQuery] string fechaInicioStr, [FromQuery] string fechaFinStr)
    {
        // Validamos las fechas de entrada
        if (!DateOnly.TryParse(fechaInicioStr, out var fechaInicio) || !DateOnly.TryParse(fechaFinStr, out var fechaFin))
        {
            return BadRequest("Formato de fecha inválido. Use AAAA-MM-DD.");
        }

        // 1. Llamamos al servicio de unificación para obtener los datos procesados.
        var datosConsolidados = await _unificationService.GenerarReporteConsolidado(fechaInicio, fechaFin);

        // 2. Pasamos esos datos al servicio de exportación para obtener el archivo Excel.
        var archivoBytes = _excelExportService.ExportarAExcel(datosConsolidados, fechaInicio, fechaFin);

        string nombreArchivo = $"ReporteAsistencia_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.xlsx";

        // 3. Devolvemos el archivo para que el usuario lo pueda descargar.
        return File(archivoBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
    }

    [HttpGet("visualizar-asistencia")]
    public async Task<IActionResult> VisualizarAsistencia([FromQuery] string fechaInicioStr, [FromQuery] string fechaFinStr)
    {
        if (!DateOnly.TryParse(fechaInicioStr, out var fechaInicio) || !DateOnly.TryParse(fechaFinStr, out var fechaFin))
        {
            return BadRequest("Formato de fecha inválido. Use AAAA-MM-DD.");
        }

        try
        {
            var datosConsolidados = await _unificationService.GenerarReporteConsolidado(fechaInicio, fechaFin);
            return Ok(datosConsolidados); // Devuelve los datos como JSON
        }
        catch (Exception ex)
        {
            // En un caso real, aquí se registraría el error
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }
}