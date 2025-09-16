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
    /// <summary>
    /// Servicio encargado de consolidar los registros de asistencia de dos fuentes distintas:
    /// el sistema SEAT y el sistema Biométrico.
    /// </summary>
    public class UnificationService
    {
        private readonly ApplicationDbContext _dbContext;

        public UnificationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Genera un reporte consolidado de asistencia para un rango de fechas específico.
        /// Este método es robusto contra problemas de zona horaria al forzar todas las
        /// comparaciones de fecha a realizarse en UTC.
        /// </summary>
        /// <param name="fechaInicio">La fecha de inicio del reporte (inclusiva).</param>
        /// <param name="fechaFin">La fecha de fin del reporte (inclusiva).</param>
        /// <returns>Una lista de registros de asistencia consolidados.</returns>
        public async Task<List<RegistroConsolidado>> GenerarReporteConsolidado(DateOnly fechaInicio, DateOnly fechaFin)
        {
            // 1. ESTABLECER RANGO DE FECHAS EN UTC PARA LA CONSULTA A LA BASE DE DATOS
            // Se crea un rango explícito en UTC para evitar conversiones de zona horaria inesperadas.
            // El rango es inclusivo para la fecha de inicio y exclusivo para la fecha de fin.
            var inicioDtUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var finExclusivo = fechaFin.AddDays(1);
            var finDtUtc = new DateTime(finExclusivo.Year, finExclusivo.Month, finExclusivo.Day, 0, 0, 0, DateTimeKind.Utc);

            // Se obtienen todos los registros relevantes de la base de datos dentro del rango UTC.
            var todosLosSeat = await _dbContext.SeatData
                .Where(s => s.HoraEntrada.HasValue && s.HoraEntrada >= inicioDtUtc && s.HoraEntrada < finDtUtc)
                .ToListAsync();

            var todosLosBiometrico = await _dbContext.BiometricoData
                .Where(b => b.Hora.HasValue && b.Hora >= inicioDtUtc && b.Hora < finDtUtc)
                .ToListAsync();

            // 2. PREPARACIÓN PARA LA UNIFICACIÓN
            var resultadoFinal = new List<RegistroConsolidado>();
            var empleadosBiometricoUnicos = todosLosBiometrico.Select(b => new { b.Nombre, b.Apellido }).Distinct().ToList();
            var empleadosSeatUnicos = todosLosSeat.Select(s => new { s.Nombre, s.Apellido }).Distinct().ToList();
            
            // Diccionario para mapear nombres del biométrico (clave) a nombres del SEAT (valor).
            var mapaDeNombres = new Dictionary<string, string>();
            var nombresBiometricoMapeados = new HashSet<string>();

            // 3. CONSTRUCCIÓN DEL MAPA DE IDENTIDAD
            // Se comparan los nombres de ambas fuentes para encontrar coincidencias y unificar identidades.
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

            // 4. PROCESAMIENTO Y CONSOLIDACIÓN DE REGISTROS
            // Se itera sobre cada día y cada empleado para construir el registro consolidado.
            for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
            {
                // Se define el rango UTC para el día específico que se está procesando.
                var inicioDelDiaUtc = new DateTime(dia.Year, dia.Month, dia.Day, 0, 0, 0, DateTimeKind.Utc);
                var finDelDiaUtc = inicioDelDiaUtc.AddDays(1);

                foreach (var empSeat in empleadosSeatUnicos)
                {
                    string nombreCompletoSeat = (empSeat.Apellido + " " + empSeat.Nombre).Trim();
                    string nombreNormalizadoSeat = NormalizeString(nombreCompletoSeat);

                    // Se buscan los registros del día actual comparando dentro del rango UTC en la lista ya cargada en memoria.
                    var registroSeatDelDia = todosLosSeat.FirstOrDefault(s =>
                        s.Apellido == empSeat.Apellido && s.Nombre == empSeat.Nombre &&
                        s.HoraEntrada.HasValue &&
                        s.HoraEntrada.Value >= inicioDelDiaUtc && s.HoraEntrada.Value < finDelDiaUtc);

                    string? nombreBiometricoAsociado = mapaDeNombres.FirstOrDefault(kvp => kvp.Value == nombreNormalizadoSeat).Key;
                    var marcacionesBiometricoDelDia = new List<BiometricoData>();

                    if (nombreBiometricoAsociado != null)
                    {
                        marcacionesBiometricoDelDia = todosLosBiometrico
                            .Where(b => b.Hora.HasValue &&
                                        b.Hora.Value >= inicioDelDiaUtc && b.Hora.Value < finDelDiaUtc &&
                                        NormalizeString(b.Nombre + " " + b.Apellido) == nombreBiometricoAsociado)
                            .OrderBy(b => b.Hora).ToList();
                    }

                    if (registroSeatDelDia == null && !marcacionesBiometricoDelDia.Any()) continue;

                    var registro = CrearRegistroConsolidado(nombreCompletoSeat, dia, registroSeatDelDia, marcacionesBiometricoDelDia);
                    resultadoFinal.Add(registro);
                }
            }

            // 5. PROCESAMIENTO DE EMPLEADOS QUE SOLO EXISTEN EN EL BIOMÉTRICO
            var empleadosSoloBiometrico = empleadosBiometricoUnicos.Where(emp => !nombresBiometricoMapeados.Contains(NormalizeString(emp.Nombre + " " + emp.Apellido)));
            foreach (var empBio in empleadosSoloBiometrico)
            {
                string nombreCompletoBio = (empBio.Nombre + " " + empBio.Apellido).Trim();
                for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
                {
                    var inicioDelDiaUtc = new DateTime(dia.Year, dia.Month, dia.Day, 0, 0, 0, DateTimeKind.Utc);
                    var finDelDiaUtc = inicioDelDiaUtc.AddDays(1);

                    var marcacionesDelDia = todosLosBiometrico
                        .Where(b => b.Nombre == empBio.Nombre && b.Apellido == empBio.Apellido &&
                                    b.Hora.HasValue &&
                                    b.Hora.Value >= inicioDelDiaUtc && b.Hora.Value < finDelDiaUtc)
                        .OrderBy(b => b.Hora).ToList();

                    if (!marcacionesDelDia.Any()) continue;

                    var registro = CrearRegistroConsolidado(nombreCompletoBio, dia, null, marcacionesDelDia);
                    resultadoFinal.Add(registro);
                }
            }

            return resultadoFinal.OrderBy(r => r.NombreCompleto).ThenBy(r => r.Fecha).ToList();
        }

        /// <summary>
        /// Crea un único objeto <see cref="RegistroConsolidado"/> a partir de los datos de SEAT y Biométrico para un día específico.
        /// </summary>
        private RegistroConsolidado CrearRegistroConsolidado(string nombrePrincipal, DateOnly dia, SeatData? registroSeat, List<BiometricoData> marcacionesBio)
        {
            var registro = new RegistroConsolidado
            {
                Area = registroSeat?.Area ?? "Área no disponible",
                NombreCompleto = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombrePrincipal.ToLower()),
                Fecha = dia,
                TipoPermiso = registroSeat?.TipoPermiso
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
            else
            {
                registro.Estado = "Sin Actividad";
            }

            return registro;
        }

        /// <summary>
        /// Calcula una puntuación de coincidencia entre dos nombres para determinar si pertenecen a la misma persona.
        /// </summary>
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

        /// <summary>
        /// Normaliza un string para comparación: lo convierte a minúsculas, quita tildes y la letra 'ñ'.
        /// </summary>
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