using OfficeOpenXml;
using System.IO;
using System.Net.Http.Json;
using System.Globalization;
using ef_core.DTOs; // Asegúrate de que esta referencia sea correcta

Console.WriteLine("WorkerBiometrico: Leyendo archivos de Excel...");


OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

try
{
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_biometrico"; 
    string[] archivosExportados = Directory.GetFiles(carpetaReportes, "*.xlsx");

    if (archivosExportados.Length > 0)
    {
        Console.WriteLine($"Se encontraron {archivosExportados.Length} archivos. Procesando...");

        using (var httpClient = new HttpClient())
        {
            string apiUrl = "http://localhost:5165/swagger/index.html"; // <--- Asegúrate del puerto

            foreach (string rutaArchivo in archivosExportados)
            {
                using (var package = new ExcelPackage(new FileInfo(rutaArchivo)))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; // Asume la primera hoja
                    
                    int rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++) // Empieza en la fila 2 para ignorar los encabezados
                    {
                        // Asegúrate de que los índices de las columnas coincidan con tu reporte de Excel
                        var nombreCompleto = worksheet.Cells[row, 1].Text;
                        var apellidoCompleto = worksheet.Cells[row, 2].Text;
                        var horaEvento = DateTime.Parse(worksheet.Cells[row, 3].Text);
                        var detalleEvento = worksheet.Cells[row, 4].Text;
                        
                        // Lógica para clasificar el tipo de evento (entrada, salida, almuerzo)
                        bool esSalidaAlmuerzo = false;
                        bool esLlegadaAlmuerzo = false;
                        
                        if (horaEvento.TimeOfDay >= new TimeSpan(12, 30, 0) &&
                            horaEvento.TimeOfDay <= new TimeSpan(15, 0, 0))
                        {
                            if (detalleEvento.ToLower().Contains("salida"))
                            {
                                esSalidaAlmuerzo = true;
                            }
                            else if (detalleEvento.ToLower().Contains("entrada"))
                            {
                                esLlegadaAlmuerzo = true;
                            }
                        }

                        var biometricoDataDto = new BiometricoDataDTO
                        {
                            Nombre = nombreCompleto,
                            Apellido = apellidoCompleto,
                            Hora = horaEvento,
                            Detalle = detalleEvento,
                            EsEntrada = detalleEvento.ToLower().Contains("entrada") && !esLlegadaAlmuerzo,
                            EsSalida = detalleEvento.ToLower().Contains("salida") && !esSalidaAlmuerzo,
                            EsSalidaAlmuerzo = esSalidaAlmuerzo,
                            EsLlegadaAlmuerzo = esLlegadaAlmuerzo
                        };

                        var response = await httpClient.PostAsJsonAsync(apiUrl, biometricoDataDto);
                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"✅ Registro de {nombreCompleto} enviado correctamente.");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Error al enviar registro de {nombreCompleto}: {response.StatusCode}");
                        }
                    }
                }
            }
        }
    }
    else
    {
        Console.WriteLine("No se encontraron archivos de Excel para procesar.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Ocurrió un error inesperado: {ex.Message}");
}
