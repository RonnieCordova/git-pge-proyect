using ef_core.Data;
using ef_core.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ef_core.Services
{
    public class UnificationService
    {
        private readonly ApplicationDbContext _dbContext;

        public UnificationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<RegistroConsolidado>> GenerarReporteConsolidado(DateOnly fechaInicio, DateOnly fechaFin)
        {
            DateTime inicioDt = fechaInicio.ToDateTime(TimeOnly.MinValue);
            DateTime finDt = fechaFin.ToDateTime(TimeOnly.MaxValue);

            var todosLosSeat = await _dbContext.SeatData
                .Where(s => s.HoraEntrada.HasValue && s.HoraEntrada >= inicioDt && s.HoraEntrada <= finDt)
                .ToListAsync();

            var todosLosBiometrico = await _dbContext.BiometricoData
                .Where(b => b.Hora.HasValue && b.Hora >= inicioDt && b.Hora <= finDt)
                .ToListAsync();

            var resultadoFinal = new List<RegistroConsolidado>();
            var empleadosBiometricoUnicos = todosLosBiometrico.Select(b => new { b.Nombre, b.Apellido }).Distinct().ToList();
            var empleadosSeatUnicos = todosLosSeat.Select(s => new { s.Nombre, s.Apellido }).Distinct().ToList();
            
            var mapaDeNombres = new Dictionary<string, string>();
            var nombresBiometricoMapeados = new HashSet<string>();

            // --- 1. CONSTRUIR EL MAPA DE IDENTIDAD ---
            foreach (var empSeat in empleadosSeatUnicos)
            {
                string nombreCompletoSeat = NormalizeString(empSeat.Apellido + " " + empSeat.Nombre);
                string? mejorMatchBiometrico = null;
                int maxPuntuacion = 0;

                foreach (var empBio in empleadosBiometricoUnicos)
                {
                    string nombreCompletoBio = NormalizeString(empBio.Nombre + " " + empBio.Apellido);
                    if (nombresBiometricoMapeados.Contains(nombreCompletoBio)) continue;

                    int puntuacionActual = GetMatchScore(nombreCompletoSeat, nombreCompletoBio);
                    if (puntuacionActual > maxPuntuacion)
                    {
                        maxPuntuacion = puntuacionActual;
                        mejorMatchBiometrico = nombreCompletoBio;
                    }
                }

                if (mejorMatchBiometrico != null && maxPuntuacion > 1)
                {
                    mapaDeNombres[mejorMatchBiometrico] = nombreCompletoSeat;
                    nombresBiometricoMapeados.Add(mejorMatchBiometrico);
                }
            }

            // --- 2. UNIFICAR DATOS USANDO EL MAPA ---
            for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
            {
                foreach (var empSeat in empleadosSeatUnicos)
                {
                    string nombreCompletoSeat = (empSeat.Apellido + " " + empSeat.Nombre).Trim();
                    string nombreNormalizadoSeat = NormalizeString(nombreCompletoSeat);

                    var registroSeatDelDia = todosLosSeat.FirstOrDefault(s => s.Apellido == empSeat.Apellido && s.Nombre == empSeat.Nombre && s.HoraEntrada.HasValue && DateOnly.FromDateTime(s.HoraEntrada.Value) == dia);
                    
                    string? nombreBiometricoAsociado = mapaDeNombres.FirstOrDefault(kvp => kvp.Value == nombreNormalizadoSeat).Key;
                    var marcacionesBiometricoDelDia = new List<BiometricoData>();

                    if(nombreBiometricoAsociado != null)
                    {
                        marcacionesBiometricoDelDia = todosLosBiometrico
                            .Where(b => b.Hora.HasValue && DateOnly.FromDateTime(b.Hora.Value) == dia && NormalizeString(b.Nombre + " " + b.Apellido) == nombreBiometricoAsociado)
                            .OrderBy(b => b.Hora).ToList();
                    }

                    if (registroSeatDelDia == null && !marcacionesBiometricoDelDia.Any()) continue;

                    var registro = CrearRegistroConsolidado(nombreCompletoSeat, dia, registroSeatDelDia, marcacionesBiometricoDelDia);
                    resultadoFinal.Add(registro);
                }
            }

            // --- 3. PROCESAR EMPLEADOS QUE SOLO ESTÁN EN EL BIOMÉTRICO ---
            var empleadosSoloBiometrico = empleadosBiometricoUnicos.Where(emp => !nombresBiometricoMapeados.Contains(NormalizeString(emp.Nombre + " " + emp.Apellido)));
            foreach (var empBio in empleadosSoloBiometrico)
            {
                string nombreCompletoBio = (empBio.Nombre + " " + empBio.Apellido).Trim();
                for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
                {
                    var marcacionesDelDia = todosLosBiometrico
                        .Where(b => b.Nombre == empBio.Nombre && b.Apellido == empBio.Apellido && b.Hora.HasValue && DateOnly.FromDateTime(b.Hora.Value) == dia)
                        .OrderBy(b => b.Hora).ToList();

                    if (!marcacionesDelDia.Any()) continue;

                    var registro = CrearRegistroConsolidado(nombreCompletoBio, dia, null, marcacionesDelDia);
                    resultadoFinal.Add(registro);
                }
            }

            return resultadoFinal.OrderBy(r => r.NombreCompleto).ThenBy(r => r.Fecha).ToList();
        }

        private RegistroConsolidado CrearRegistroConsolidado(string nombrePrincipal, DateOnly dia, SeatData? registroSeat, List<BiometricoData> marcacionesBio)
        {
            var registro = new RegistroConsolidado
            {
                NombreCompleto = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombrePrincipal.ToLower()),
                Fecha = dia
            };

            if (registroSeat != null)
            {
                if (registroSeat.HoraEntrada.HasValue) { registro.HoraEntrada = registroSeat.HoraEntrada; registro.Fuentes.Add("Entrada: SEAT"); }
                if (registroSeat.HoraSalidaAlmuerzo.HasValue) { registro.HoraSalidaAlmuerzo = registroSeat.HoraSalidaAlmuerzo; registro.Fuentes.Add("Salida Almuerzo: SEAT"); }
                if (registroSeat.HoraRegresoAlmuerzo.HasValue) { registro.HoraRegresoAlmuerzo = registroSeat.HoraRegresoAlmuerzo; registro.Fuentes.Add("Regreso Almuerzo: SEAT"); }
                if (registroSeat.HoraSalida.HasValue) { registro.HoraSalida = registroSeat.HoraSalida; registro.Fuentes.Add("Salida: SEAT"); }
            }

            if (marcacionesBio.Any())
            {
                if (registro.HoraEntrada == null && marcacionesBio.FirstOrDefault(b => b.EsEntrada)?.Hora is DateTime hEntrada) { registro.HoraEntrada = hEntrada; registro.Fuentes.Add("Entrada: Biométrico"); }
                if (registro.HoraSalidaAlmuerzo == null && marcacionesBio.FirstOrDefault(b => b.EsSalidaAlmuerzo)?.Hora is DateTime hSalidaAlmuerzo) { registro.HoraSalidaAlmuerzo = hSalidaAlmuerzo; registro.Fuentes.Add("Salida Almuerzo: Biométrico"); }
                if (registro.HoraRegresoAlmuerzo == null && marcacionesBio.FirstOrDefault(b => b.EsLlegadaAlmuerzo)?.Hora is DateTime hRegresoAlmuerzo) { registro.HoraRegresoAlmuerzo = hRegresoAlmuerzo; registro.Fuentes.Add("Regreso Almuerzo: Biométrico"); }
                if (registro.HoraSalida == null && marcacionesBio.LastOrDefault(b => b.EsSalida)?.Hora is DateTime hSalida) { registro.HoraSalida = hSalida; registro.Fuentes.Add("Salida: Biométrico"); }
            }

            if (registro.Fuentes.Any())
            {
                 if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue) registro.Estado = "Completo";
                else if (registro.HoraEntrada.HasValue && !registro.HoraSalida.HasValue) registro.Estado = "Falta marcación de Salida";
                else if (!registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue) registro.Estado = "Falta marcación de Entrada";
                else registro.Estado = "Registros Incompletos";
            }
            else {
                registro.Estado = "Sin Actividad";
            }

            return registro;
        }
        
        private int GetMatchScore(string nombreCompleto, string nombreParcial)
        {
            var partesParcial = nombreParcial.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int puntuacion = 0;
            foreach (var parte in partesParcial)
            {
                if (nombreCompleto.Contains(parte))
                {
                    puntuacion++;
                }
            }
            return puntuacion;
        }

        // FUNCIÓN DE NORMALIZACIÓN
        private string NormalizeString(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            
            string replacedN = input.ToLower().Replace('ñ', 'n');

            var normalizedString = replacedN.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace("  ", " ").Trim();
        }
    }
}
//http://localhost:5165/api/reportes/exportar-asistencia?fechaInicioStr=2025-08-18&fechaFinStr=2025-08-22