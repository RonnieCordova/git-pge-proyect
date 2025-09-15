using System.Drawing;
using ef_core.Data; // Asegúrate de que este 'using' apunte a donde tienes tu clase RegistroConsolidado
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ef_core.Services;// O el namespace que corresponda a tus servicios

    public class ExcelExportService
    {
        /// <summary>
        /// Genera un archivo de Excel a partir de una lista de registros consolidados.
        /// </summary>
        /// <param name="registros">La lista de datos de asistencia unificados.</param>
        /// <param name="fechaInicio">Fecha de inicio del reporte para el título.</param>
        /// <param name="fechaFin">Fecha de fin del reporte para el título.</param>
        /// <returns>Un array de bytes que representa el archivo .xlsx.</returns>
        public byte[] ExportarAExcel(List<RegistroConsolidado> registros, DateOnly fechaInicio, DateOnly fechaFin)
        {
            using (var paquete = new ExcelPackage())
            {
                var hoja = paquete.Workbook.Worksheets.Add("Reporte de Asistencia");

                // TÍTULO DEL REPORTE
                hoja.Cells["A1:H1"].Merge = true;
                hoja.Cells["A1"].Value = $"Reporte de Asistencia Consolidado ({fechaInicio} al {fechaFin})";
                hoja.Cells["A1"].Style.Font.Bold = true;
                hoja.Cells["A1"].Style.Font.Size = 16;
                hoja.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // CABECERAS DE LA TABLA
                hoja.Cells["A3"].Value = "Área";
                hoja.Cells["B3"].Value = "Empleado";
                hoja.Cells["C3"].Value = "Fecha";
                hoja.Cells["D3"].Value = "Hora Entrada";
                hoja.Cells["E3"].Value = "Salida Almuerzo";
                hoja.Cells["F3"].Value = "Regreso Almuerzo";
                hoja.Cells["G3"].Value = "Hora Salida";
                hoja.Cells["H3"].Value = "Estado";
                hoja.Cells["I3"].Value = "Fuentes";
                hoja.Cells["J3"].Value = "Tipo de permiso";
                // Aplicar estilo a las cabeceras
            using (var rango = hoja.Cells["A3:I3"])
            {
                rango.Style.Font.Bold = true;
                rango.Style.Fill.PatternType = ExcelFillStyle.Solid;
                rango.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

                // RELLENAR DATOS
                int filaActual = 4;
                foreach (var registro in registros)
                {
                    hoja.Cells[filaActual, 1].Value = registro.Area;
                    hoja.Cells[filaActual, 2].Value = registro.NombreCompleto;
                    hoja.Cells[filaActual, 3].Value = registro.Fecha.ToString("dd/MM/yyyy");
                    hoja.Cells[filaActual, 4].Value = registro.HoraEntrada?.ToString("HH:mm");
                    hoja.Cells[filaActual, 5].Value = registro.HoraSalidaAlmuerzo?.ToString("HH:mm");
                    hoja.Cells[filaActual, 6].Value = registro.HoraRegresoAlmuerzo?.ToString("HH:mm");
                    hoja.Cells[filaActual, 7].Value = registro.HoraSalida?.ToString("HH:mm");
                    hoja.Cells[filaActual, 8].Value = registro.Estado;
                    hoja.Cells[filaActual, 9].Value = string.Join(", ", registro.Fuentes);
                    hoja.Cells[filaActual, 10].Value = registro.TipoPermiso;
                    filaActual++;
                }

                hoja.Cells[hoja.Dimension.Address].AutoFitColumns();

                return paquete.GetAsByteArray();
            }
        }
    }
