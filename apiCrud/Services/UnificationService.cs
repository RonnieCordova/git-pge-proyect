using ef_core.Data;
using ef_core.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ef_core.Services;
public class UnificationService
{
    private readonly ApplicationDbContext _dbContext;

    public UnificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RegistroConsolidado>> GenerarReporteConsolidado(DateOnly fechaInicio, DateOnly fechaFin)
    {
        var resultadoFinal = new List<RegistroConsolidado>();

        DateTime inicioDt = fechaInicio.ToDateTime(TimeOnly.MinValue);
        DateTime finDt = fechaFin.ToDateTime(TimeOnly.MaxValue);

        var todosLosSeat = await _dbContext.SeatData
            .Where(s => s.HoraEntrada.HasValue && s.HoraEntrada >= inicioDt && s.HoraEntrada <= finDt)
            .ToListAsync();

        var todosLosBiometrico = await _dbContext.BiometricoData
            .Where(b => b.Hora.HasValue && b.Hora >= inicioDt && b.Hora <= finDt)
            .ToListAsync();

        var empleadosSeat = todosLosSeat.Select(s => NormalizeString((s.Nombre + " " + s.Apellido).Trim())).Distinct();
        var empleadosBiometrico = todosLosBiometrico.Select(b => NormalizeString((b.Nombre + " " + b.Apellido).Trim())).Distinct();
        var todosLosNombresNormalizados = empleadosSeat.Union(empleadosBiometrico).Distinct();

        foreach (var nombreNormalizado in todosLosNombresNormalizados)
        {
            for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
            {
                var registroSeat = todosLosSeat.FirstOrDefault(s => s.HoraEntrada.HasValue && NormalizeString((s.Nombre + " " + s.Apellido).Trim()) == nombreNormalizado && DateOnly.FromDateTime(s.HoraEntrada.Value) == dia);

                var registrosBiometricoDelDia = todosLosBiometrico
                    .Where(b => b.Nombre != null && b.Apellido != null && b.Hora.HasValue &&
                                nombreNormalizado.Contains(NormalizeString(b.Nombre)) &&
                                nombreNormalizado.Contains(NormalizeString(b.Apellido)) &&
                                DateOnly.FromDateTime(b.Hora.Value) == dia)
                    .OrderBy(b => b.Hora)
                    .ToList();

                if (registroSeat == null && !registrosBiometricoDelDia.Any())
                {
                    continue;
                }

                string nombrePrincipal = registroSeat != null ? (registroSeat.Nombre + " " + registroSeat.Apellido).Trim() : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombreNormalizado);

                var registro = new RegistroConsolidado
                {
                    NombreCompleto = nombrePrincipal,
                    Fecha = dia
                };

                if (registroSeat != null)
                {
                    if (registroSeat.HoraEntrada.HasValue) { registro.HoraEntrada = registroSeat.HoraEntrada; registro.Fuentes.Add("Entrada: SEAT"); }
                    if (registroSeat.HoraSalidaAlmuerzo.HasValue) { registro.HoraSalidaAlmuerzo = registroSeat.HoraSalidaAlmuerzo; registro.Fuentes.Add("Salida Almuerzo: SEAT"); }
                    if (registroSeat.HoraRegresoAlmuerzo.HasValue) { registro.HoraRegresoAlmuerzo = registroSeat.HoraRegresoAlmuerzo; registro.Fuentes.Add("Regreso Almuerzo: SEAT"); }
                    if (registroSeat.HoraSalida.HasValue) { registro.HoraSalida = registroSeat.HoraSalida; registro.Fuentes.Add("Salida: SEAT"); }
                }

                if (registrosBiometricoDelDia.Any())
                {
                    // CORRECCIÓN: Se usan nombres de variable únicos (hEntrada, hSalidaAlmuerzo, etc.)
                    if (registro.HoraEntrada == null && registrosBiometricoDelDia.FirstOrDefault(b => b.EsEntrada)?.Hora is DateTime hEntrada) { registro.HoraEntrada = hEntrada; registro.Fuentes.Add("Entrada: Biométrico"); }
                    if (registro.HoraSalidaAlmuerzo == null && registrosBiometricoDelDia.FirstOrDefault(b => b.EsSalidaAlmuerzo)?.Hora is DateTime hSalidaAlmuerzo) { registro.HoraSalidaAlmuerzo = hSalidaAlmuerzo; registro.Fuentes.Add("Salida Almuerzo: Biométrico"); }
                    if (registro.HoraRegresoAlmuerzo == null && registrosBiometricoDelDia.FirstOrDefault(b => b.EsLlegadaAlmuerzo)?.Hora is DateTime hRegresoAlmuerzo) { registro.HoraRegresoAlmuerzo = hRegresoAlmuerzo; registro.Fuentes.Add("Regreso Almuerzo: Biométrico"); }
                    if (registro.HoraSalida == null && registrosBiometricoDelDia.FirstOrDefault(b => b.EsSalida)?.Hora is DateTime hSalida) { registro.HoraSalida = hSalida; registro.Fuentes.Add("Salida: Biométrico"); }
                }

                if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue) registro.Estado = "Completo";
                else if (registro.HoraEntrada.HasValue && !registro.HoraSalida.HasValue) registro.Estado = "Falta marcación de Salida";
                else if (!registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue) registro.Estado = "Falta marcación de Entrada";
                else registro.Estado = "Registros Incompletos";

                resultadoFinal.Add(registro);
            }
        }
        return resultadoFinal.OrderBy(r => r.NombreCompleto).ThenBy(r => r.Fecha).ToList();
    }

    private string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var normalizedString = input.ToLower().Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        foreach (var c in normalizedString.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            stringBuilder.Append(c);
        }
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace("  ", " ");
    }
}