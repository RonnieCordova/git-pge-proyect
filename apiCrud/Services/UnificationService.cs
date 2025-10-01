using apiCrud.Migrations;
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
    /// Servicio que consolida registros de asistencia y aplica reglas de negocio complejas,
    /// incluyendo la gestión de diferentes tipos de permisos.
    /// </summary>
    public class UnificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private static readonly TimeSpan HoraDeEntradaOficial = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan VentanaToleranciaEntrada = new TimeSpan(0, 10, 0);
        private static readonly TimeSpan HoraDeEntradaConMargen = new TimeSpan(8, 40, 0);
        private static readonly TimeSpan HoraDeSalidaOficial = new TimeSpan(17, 0, 0);

        public UnificationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Genera un reporte consolidado de asistencia para un rango de fechas.
        /// </summary>
        public async Task<List<RegistroConsolidado>> GenerarReporteConsolidado(DateOnly fechaInicio, DateOnly fechaFin, string? nombre = null)
        {
            // 1. OBTENCIÓN DE DATOS CRUDOS EN UTC
            var inicioDtUtc = new DateTime(fechaInicio.Year, fechaInicio.Month, fechaInicio.Day, 0, 0, 0, DateTimeKind.Utc);
            var finExclusivo = fechaFin.AddDays(1);
            var finDtUtc = new DateTime(finExclusivo.Year, finExclusivo.Month, finExclusivo.Day, 0, 0, 0, DateTimeKind.Utc);

            var todosLosSeat = await _dbContext.SeatData
                .Where(s => s.HoraEntrada.HasValue && s.HoraEntrada >= inicioDtUtc && s.HoraEntrada < finDtUtc)
                .ToListAsync();

            var todosLosBiometrico = await _dbContext.BiometricoData
                .Where(b => b.Hora.HasValue && b.Hora >= inicioDtUtc && b.Hora < finDtUtc)
                .ToListAsync();

            // 2. PREPARACIÓN Y PROCESAMIENTO
            var resultadoFinal = new List<RegistroConsolidado>();
            var empleadosSeatUnicos = todosLosSeat.Select(s => new { s.Nombre, s.Apellido }).Distinct().ToList();

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                // Normalizamos el nombre del filtro y el de la lista para una comparación robusta
                string nombreFiltroNormalizado = NormalizeString(nombre);
                empleadosSeatUnicos = empleadosSeatUnicos
                    .Where(emp => NormalizeString(emp.Apellido + " " + emp.Nombre) == nombreFiltroNormalizado)
                    .ToList();
            }
            // Construimos un mapa de identidad para asociar nombres entre el biométrico y SIATH
            var mapaDeNombres = ConstruirMapaDeIdentidad(empleadosSeatUnicos, todosLosBiometrico);

            for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
            {
                var inicioDelDiaUtc = new DateTime(dia.Year, dia.Month, dia.Day, 0, 0, 0, DateTimeKind.Utc);
                var finDelDiaUtc = inicioDelDiaUtc.AddDays(1);

                foreach (var empSeat in empleadosSeatUnicos)
                {
                    string nombreCompletoSeat = (empSeat.Apellido + " " + empSeat.Nombre).Trim();
                    string nombreNormalizadoSeat = NormalizeString(nombreCompletoSeat);

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

                    var registro = ProcesarDiaDeEmpleado(nombreCompletoSeat, dia, registroSeatDelDia, marcacionesBiometricoDelDia);
                    resultadoFinal.Add(registro);
                }
            }
            
            return resultadoFinal.OrderBy(r => r.NombreCompleto).ThenBy(r => r.Fecha).ToList();
        }

        /// <summary>
        /// Método principal que orquesta la aplicación de reglas de negocio para un empleado en un día.
        /// </summary>
        private RegistroConsolidado ProcesarDiaDeEmpleado(string nombrePrincipal, DateOnly dia, SeatData? registroSeat, List<BiometricoData> marcacionesBio)
        {
            var registro = new RegistroConsolidado
            {
                Area = registroSeat?.Area ?? "Área no disponible",
                NombreCompleto = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombrePrincipal.ToLower()),
                Fecha = dia,
                TipoPermiso = registroSeat?.TipoPermiso
            };

            var permiso = IdentificarTipoPermiso(registro.TipoPermiso);

            var todasLasMarcaciones = marcacionesBio.Select(m => m.Hora.Value)
                .Union(registroSeat != null ? new[] { registroSeat.HoraEntrada, registroSeat.HoraSalidaAlmuerzo, registroSeat.HoraRegresoAlmuerzo, registroSeat.HoraSalida }.Where(h => h.HasValue).Select(h => h.Value) : Enumerable.Empty<DateTime>())
                .Distinct().OrderBy(h => h).ToList();

            // REGLA DE PRIORIDAD #1: Si existe un permiso, se usa la lógica de permisos.
            if (permiso != PermisoTipo.Ninguno)
            {
                AplicarLogicaDePermiso(registro, todasLasMarcaciones, permiso);
            }
            // REGLA DE PRIORIDAD #2: Si no hay permiso, se usa la lógica estándar.
            else
            {
                AplicarLogicaEstandar(registro, registroSeat, marcacionesBio);
            }

            // Se determina el estado final del registro.
            if (string.IsNullOrEmpty(registro.Estado))
            {
                if (!registro.HoraEntrada.HasValue && !registro.HoraSalida.HasValue)
                {
                    registro.Estado = "Sin Actividad";
                }
                else if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue)
                {
                    registro.Estado = "Completo";
                }
                else
                {
                    registro.Estado = "Registros Incompletos";
                }
            }

            return registro;
        }

        /// <summary>
        /// Aplica la lógica de negocio cuando se detecta un permiso especial que altera la jornada.
        /// </summary>
        private void AplicarLogicaDePermiso(RegistroConsolidado registro, List<DateTime> todasLasMarcaciones, PermisoTipo permiso)
        {
            if (!todasLasMarcaciones.Any()) return;

            registro.HoraEntrada = todasLasMarcaciones.First();
            registro.Fuentes.Add("Entrada: Registrada");

            // Helper local: calcula horas no trabajadas tomando 08:40 como ancla de entrada
            // (10min tolerancia) y 17:00 como ancla de salida.
            TimeSpan CalcularHorasNoTrabajadas(DateTime entrada, DateTime salida)
            {
                var horaEntrada = entrada.TimeOfDay;
                var horaSalida = salida.TimeOfDay;

                // Si completó o excedió la jornada (llegó a o antes de 08:40 y salió a o después de 17:00) => 00:00
                if (horaEntrada <= HoraDeEntradaConMargen && horaSalida >= HoraDeSalidaOficial)
                {
                    return TimeSpan.Zero;
                }

                var noTrabajadas = TimeSpan.Zero;

                // Llegada tarde respecto a 08:40
                if (horaEntrada > HoraDeEntradaConMargen)
                {
                    noTrabajadas += horaEntrada - HoraDeEntradaConMargen;
                }

                // Salida temprana respecto a 17:00
                if (horaSalida < HoraDeSalidaOficial)
                {
                    noTrabajadas += HoraDeSalidaOficial - horaSalida;
                }

                return noTrabajadas;
            }

            // Para el permiso "Oficial", cualquier segunda marcación es la salida final.
            if (permiso == PermisoTipo.Oficial)
            {
                if (todasLasMarcaciones.Count > 1)
                {
                    registro.HoraSalida = todasLasMarcaciones.Skip(1).First();
                    registro.Fuentes.Add("Salida: Registrada (Permiso Oficial)");
                    registro.Estado = $"Jornada completada por permiso '{registro.TipoPermiso}'";

                    if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue)
                    {
                        registro.HorasNoTrabajadas = CalcularHorasNoTrabajadas(registro.HoraEntrada.Value, registro.HoraSalida.Value);
                    }
                }
                else
                {
                    registro.Estado = $"Entrada registrada, salida por permiso oficial pendiente";
                }
                return;
            }

            // Para otros permisos (Asuntos Personales, Cita Médica, etc.)
            // La última marcación del día se considera la salida final.
            if (todasLasMarcaciones.Count > 1)
            {
                registro.HoraSalida = todasLasMarcaciones.Last();
                registro.Fuentes.Add("Salida: Registrada (Permiso)");

                var duracionJornada = registro.HoraSalida.Value - registro.HoraEntrada.Value;
                registro.Estado = $"Jornada con permiso '{registro.TipoPermiso}'({duracionJornada.Hours}h {duracionJornada.Minutes}m)";

                if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue)
                {
                    registro.HorasNoTrabajadas = CalcularHorasNoTrabajadas(registro.HoraEntrada.Value, registro.HoraSalida.Value);
                }
            }
            else
            {
                registro.Estado = $"Entrada registrada, salida por permiso pendiente";
            }
        }
        

        /// <summary>
        /// Aplica la lógica estándar de unificación, incluyendo la reconciliación de horas de entrada.
        /// </summary>
        private void AplicarLogicaEstandar(RegistroConsolidado registro, SeatData? registroSeat, List<BiometricoData> marcacionesBio)
        {
            var horaEntradaSiath = registroSeat?.HoraEntrada;
            var horaEntradaBio = marcacionesBio.FirstOrDefault(b => b.EsEntrada)?.Hora;
            registro.HoraEntrada = ReconciliarHoraEntrada(horaEntradaSiath, horaEntradaBio, registro.Fuentes);

            var marcacionesAlmuerzo = marcacionesBio.Where(m => m.EsSalidaAlmuerzo || m.EsLlegadaAlmuerzo).OrderBy(m => m.Hora).ToList();

            registro.HoraSalidaAlmuerzo = registroSeat?.HoraSalidaAlmuerzo ?? marcacionesAlmuerzo.FirstOrDefault(m => m.EsSalidaAlmuerzo)?.Hora;
            registro.HoraRegresoAlmuerzo = registroSeat?.HoraRegresoAlmuerzo ?? marcacionesAlmuerzo.FirstOrDefault(m => m.EsLlegadaAlmuerzo)?.Hora;
            registro.HoraSalida = registroSeat?.HoraSalida ?? marcacionesBio.LastOrDefault(b => b.EsSalida)?.Hora;

            if (registroSeat?.HoraSalidaAlmuerzo != null) registro.Fuentes.Add("Salida Almuerzo: SIATH");
            else if (marcacionesAlmuerzo.Any(m => m.EsSalidaAlmuerzo)) registro.Fuentes.Add("Salida Almuerzo: Biométrico");

            if (registroSeat?.HoraRegresoAlmuerzo != null) registro.Fuentes.Add("Regreso Almuerzo: SIATH");
            else if (marcacionesAlmuerzo.Any(m => m.EsLlegadaAlmuerzo)) registro.Fuentes.Add("Regreso Almuerzo: Biométrico");

            if (registroSeat?.HoraSalida != null) registro.Fuentes.Add("Salida: SIATH");
            else if (marcacionesBio.Any(b => b.EsSalida)) registro.Fuentes.Add("Salida: Biométrico");

            // Lógica local para calcular horas no trabajadas respecto a 08:40 - 17:00
            if (registro.HoraEntrada.HasValue && registro.HoraSalida.HasValue)
            {
                var horaEntrada = registro.HoraEntrada.Value.TimeOfDay;
                var horaSalida = registro.HoraSalida.Value.TimeOfDay;

                // Si completó la jornada o la excedió => 00:00
                if (horaEntrada <= HoraDeEntradaConMargen && horaSalida >= HoraDeSalidaOficial)
                {
                    registro.HorasNoTrabajadas = TimeSpan.Zero;
                }
                else
                {
                    var noTrabajadas = TimeSpan.Zero;

                    if (horaEntrada > HoraDeEntradaConMargen)
                    {
                        noTrabajadas += horaEntrada - HoraDeEntradaConMargen;
                    }

                    if (horaSalida < HoraDeSalidaOficial)
                    {
                        noTrabajadas += HoraDeSalidaOficial - horaSalida;
                    }

                    registro.HorasNoTrabajadas = noTrabajadas;
                }
            }
        }

        /// <summary>
        /// Determina la hora de entrada oficial basándose en las reglas de negocio.
        /// </summary>
        private DateTime? ReconciliarHoraEntrada(DateTime? horaSiath, DateTime? horaBio, List<string> fuentes)
        {
            if (!horaSiath.HasValue && !horaBio.HasValue) return null;
            if (!horaSiath.HasValue) { fuentes.Add("Entrada: Biométrico"); return horaBio; }
            if (!horaBio.HasValue) { fuentes.Add("Entrada: SIATH"); return horaSiath; }

            var timeSiath = horaSiath.Value.TimeOfDay;
            var timeBio = horaBio.Value.TimeOfDay;

            if (timeSiath > HoraDeEntradaOficial.Add(VentanaToleranciaEntrada) && timeBio <= HoraDeEntradaOficial.Add(VentanaToleranciaEntrada))
            {
                fuentes.Add("Entrada: Biométrico (Priorizado)");
                return horaBio;
            }

            fuentes.Add("Entrada: SIATH (Priorizado)");
            return horaSiath;
        }
        
        /// <summary>
        /// Enum para representar los tipos de permiso de forma estructurada.
        /// </summary>
        private enum PermisoTipo { Ninguno, AsuntoPersonal, Calamidad, Enfermedad, CitaMedica, Rehabilitacion, Oficial }
        
        /// <summary>
        /// Analiza el texto del permiso y lo clasifica en un tipo estructurado.
        /// </summary>
        private PermisoTipo IdentificarTipoPermiso(string? tipoPermiso)
        {
            if (string.IsNullOrWhiteSpace(tipoPermiso)) return PermisoTipo.Ninguno;

            string permisoUpper = tipoPermiso.ToUpperInvariant();

            if (permisoUpper.Contains("OFICIAL")) return PermisoTipo.Oficial;
            if (permisoUpper.Contains("ASUNTOS") && permisoUpper.Contains("PERSONALES")) return PermisoTipo.AsuntoPersonal;
            if (permisoUpper.Contains("CALAMIDAD")) return PermisoTipo.Calamidad;
            if (permisoUpper.Contains("ENFERMEDAD")) return PermisoTipo.Enfermedad;
            if (permisoUpper.Contains("CITA") && permisoUpper.Contains("MEDICA")) return PermisoTipo.CitaMedica;
            if (permisoUpper.Contains("REHABILITACION")) return PermisoTipo.Rehabilitacion;

            return PermisoTipo.Ninguno;
        }

        /// <summary>
        /// Construye un mapa de identidad para asociar nombres entre el biométrico y SIATH.
        /// </summary>
        private Dictionary<string, string> ConstruirMapaDeIdentidad(IEnumerable<dynamic> empleadosSeat, List<BiometricoData> todosLosBiometrico)
        {
            var mapa = new Dictionary<string, string>();
            var mapeados = new HashSet<string>();
            var empleadosBio = todosLosBiometrico.Select(b => new { b.Nombre, b.Apellido }).Distinct();

            foreach (var empSeat in empleadosSeat)
            {
                string nombreCompletoSeat = NormalizeString(empSeat.Apellido + " " + empSeat.Nombre);
                string? mejorMatch = null;
                int maxPuntuacion = 0;

                foreach (var empBio in empleadosBio)
                {
                    string nombreCompletoBio = NormalizeString(empBio.Nombre + " " + empBio.Apellido);
                    if (mapeados.Contains(nombreCompletoBio)) continue;
                    int puntuacion = GetMatchScore(nombreCompletoSeat, nombreCompletoBio);
                    if (puntuacion > maxPuntuacion)
                    {
                        maxPuntuacion = puntuacion;
                        mejorMatch = nombreCompletoBio;
                    }
                }
                if (mejorMatch != null && maxPuntuacion > 1)
                {
                    mapa[mejorMatch] = nombreCompletoSeat;
                    mapeados.Add(mejorMatch);
                }
            }
            return mapa;
        }

        /// <summary>
        /// Calcula una puntuación de coincidencia entre dos nombres.
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
        /// Normaliza un string para comparación (minúsculas, sin tildes, etc.).
        /// </summary>
        private string NormalizeString(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string replacedN = input.ToLower().Replace('ñ', 'n');
            var normalizedString = replacedN.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace("  ", " ").Trim();
        }
    }
}
//http://localhost:5165/api/reportes/exportar-asistencia?fechaInicioStr=2025-08-18&fechaFinStr=2025-08-22