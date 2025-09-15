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
            var resultadoFinal = new List<RegistroConsolidado>();
            DateTime inicioDt = fechaInicio.ToDateTime(TimeOnly.MinValue);
            DateTime finDt = fechaFin.ToDateTime(TimeOnly.MaxValue);

            var todosLosSeat = await _dbContext.SeatData
                .Where(s => s.HoraEntrada.HasValue && s.HoraEntrada >= inicioDt && s.HoraEntrada <= finDt)
                .ToListAsync();

            var todosLosBiometrico = await _dbContext.BiometricoData
                .Where(b => b.Hora.HasValue && b.Hora >= inicioDt && b.Hora <= finDt)
                .ToListAsync();

            var empleadosSeat = todosLosSeat.Select(s => new { s.Nombre, s.Apellido }).Distinct();
            var empleadosBiometrico = todosLosBiometrico.Select(b => new { b.Nombre, b.Apellido }).Distinct();
            var todosLosEmpleados = empleadosSeat.Union(empleadosBiometrico).Distinct();

            foreach (var empleado in todosLosEmpleados)
            {
                for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
                {
                    var registro = new RegistroConsolidado
                    {
                        NombreCompleto = CultureInfo.CurrentCulture.TextInfo.ToTitleCase($"{empleado.Apellido} {empleado.Nombre}".ToLower()),
                        Fecha = dia
                    };

                    var registroSeat = todosLosSeat.FirstOrDefault(s => s.Nombre == empleado.Nombre && s.Apellido == empleado.Apellido && s.HoraEntrada.HasValue && DateOnly.FromDateTime(s.HoraEntrada.Value) == dia);
                    
                    // --- OBTENER PERMISO Y CALCULAR JORNADA ---
                    string? tipoPermiso = registroSeat?.TipoPermiso;
                    TimeSpan jornadaEsperada = ObtenerDuracionJornada(tipoPermiso);
                    
                    if (registroSeat != null)
                    {
                        registro.HoraEntrada = registroSeat.HoraEntrada;
                        registro.HoraSalidaAlmuerzo = registroSeat.HoraSalidaAlmuerzo;
                        registro.HoraRegresoAlmuerzo = registroSeat.HoraRegresoAlmuerzo;
                        registro.HoraSalida = registroSeat.HoraSalida;
                        if(registro.HoraEntrada.HasValue) registro.Fuentes.Add("Entrada: SEAT");
                        if(registro.HoraSalidaAlmuerzo.HasValue) registro.Fuentes.Add("Salida Almuerzo: SEAT");
                        if(registro.HoraRegresoAlmuerzo.HasValue) registro.Fuentes.Add("Regreso Almuerzo: SEAT");
                        if(registro.HoraSalida.HasValue) registro.Fuentes.Add("Salida: SEAT");
                    }

                    var marcacionesBiometrico = todosLosBiometrico
                        .Where(b => b.Nombre == empleado.Nombre && b.Apellido == empleado.Apellido && b.Hora.HasValue && DateOnly.FromDateTime(b.Hora.Value) == dia)
                        .OrderBy(b => b.Hora)
                        .ToList();

                    if (marcacionesBiometrico.Any())
                    {
                        // --- LÓGICA INTELIGENTE PARA RELLENAR HUECOS ---
                        TimeSpan horaSalidaEsperada = registro.HoraEntrada?.TimeOfDay + jornadaEsperada ?? new TimeSpan(17, 0, 0);

                        if (!registro.HoraEntrada.HasValue && marcacionesBiometrico.FirstOrDefault(m => m.EsEntrada)?.Hora is DateTime h) {
                            registro.HoraEntrada = h; registro.Fuentes.Add("Entrada: Biométrico");
                        }
                        
                        // Si la última marcación es consistente con una jornada corta, es la salida final.
                        var ultimaMarcacionBio = marcacionesBiometrico.LastOrDefault(m => m.EsSalida);
                        if (!registro.HoraSalida.HasValue && ultimaMarcacionBio?.Hora is DateTime hSalida) {
                            // Si la hora de salida está cerca del fin de la jornada con permiso, es la salida final.
                            if(Math.Abs((hSalida.TimeOfDay - horaSalidaEsperada).TotalHours) < 1.5)
                            {
                                registro.HoraSalida = hSalida;
                                registro.Fuentes.Add("Salida: Biométrico");
                            }
                        }

                        // El resto de las marcaciones se evalúan para el almuerzo
                        if (!registro.HoraSalidaAlmuerzo.HasValue && marcacionesBiometrico.FirstOrDefault(m => m.EsSalidaAlmuerzo)?.Hora is DateTime hSalidaAlm) {
                            registro.HoraSalidaAlmuerzo = hSalidaAlm; registro.Fuentes.Add("Salida Almuerzo: Biométrico");
                        }
                        if (!registro.HoraRegresoAlmuerzo.HasValue && marcacionesBiometrico.FirstOrDefault(m => m.EsLlegadaAlmuerzo)?.Hora is DateTime hRegresoAlm) {
                            registro.HoraRegresoAlmuerzo = hRegresoAlm; registro.Fuentes.Add("Regreso Almuerzo: Biométrico");
                        }

                        // Si después de la lógica de permiso aún falta la salida, se asigna la última marcación.
                         if (!registro.HoraSalida.HasValue && ultimaMarcacionBio?.Hora is DateTime hSalidaFinal) {
                            registro.HoraSalida = hSalidaFinal; registro.Fuentes.Add("Salida: Biométrico");
                        }
                    }
                    
                    if (!registro.Fuentes.Any()) continue;
                    
                    registro.Estado = EvaluarEstado(registro, jornadaEsperada);
                    resultadoFinal.Add(registro);
                }
            }

            return resultadoFinal.OrderBy(r => r.NombreCompleto).ThenBy(r => r.Fecha).ToList();
        }

        // --- MOTOR DE REGLAS DE PERMISOS ---
        private TimeSpan ObtenerDuracionJornada(string? tipoPermiso)
        {
            var jornadaNormal = new TimeSpan(8, 0, 0);
            if (string.IsNullOrEmpty(tipoPermiso)) return jornadaNormal;

            string permisoLower = tipoPermiso.ToLower();

            if (permisoLower.Contains("asunto personal") || permisoLower.Contains("calamidad") || permisoLower.Contains("enfermedad"))
            {
                return new TimeSpan(4, 0, 0); // Jornada reducida a 4 horas
            }
            if (permisoLower.Contains("cita medica") || permisoLower.Contains("rehabilitacion"))
            {
                return new TimeSpan(6, 0, 0); // Jornada reducida a 6 horas (8h - 2h de permiso)
            }

            return jornadaNormal;
        }
        
        // --- EVALUADOR DE ESTADO CON CONTEXTO DE PERMISOS ---
        private string EvaluarEstado(RegistroConsolidado registro, TimeSpan jornadaEsperada)
        {
            var horaEntradaOficial = new TimeSpan(8, 30, 0);
            var margenAtraso = new TimeSpan(0, 10, 0);

            bool tienePermiso = jornadaEsperada.Hours < 8;

            if (!registro.HoraEntrada.HasValue) return "Falta sin Justificar";
            if (registro.HoraEntrada.Value.TimeOfDay > horaEntradaOficial + margenAtraso) return "Atraso";
            if (!registro.HoraSalida.HasValue) return "Falta marcación de Salida";
            
            // Si la jornada real es menor que la esperada, es incompleta.
            TimeSpan duracionReal = registro.HoraSalida.Value.TimeOfDay - registro.HoraEntrada.Value.TimeOfDay;
            if (registro.HoraRegresoAlmuerzo.HasValue && registro.HoraSalidaAlmuerzo.HasValue)
            {
                duracionReal -= (registro.HoraRegresoAlmuerzo.Value.TimeOfDay - registro.HoraSalidaAlmuerzo.Value.TimeOfDay);
            }

            if(duracionReal < jornadaEsperada - margenAtraso)
            {
                 return tienePermiso ? "Incompleto (con permiso)" : "Incompleto";
            }
            
            return tienePermiso ? "Completo (con permiso)" : "Completo";
        }
    }
}
//http://localhost:5165/api/reportes/exportar-asistencia?fechaInicioStr=2025-08-18&fechaFinStr=2025-08-22