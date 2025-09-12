using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ExcelDataReader;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using ef_core.DTOs;

Console.WriteLine("WorkerBiometrico (Versión Final): Leyendo archivos de Excel...");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_biometrico"; // Asegúrate que esta ruta sea correcta
    string[] archivosExportados = Directory.GetFiles(carpetaReportes, "*.xlsx")
                                         .Union(Directory.GetFiles(carpetaReportes, "*.xls"))
                                         .ToArray();

    if (archivosExportados.Length == 0)
    {
        Console.WriteLine("No se encontraron archivos de Excel para procesar.");
        Console.ReadKey();
        return;
    }

    Console.WriteLine($"Se encontraron {archivosExportados.Length} archivos. Procesando...");

    using var httpClient = new HttpClient();
    string apiUrl = "http://localhost:5165/api/BiometricoData"; // Asegúrate que el puerto sea el correcto

    foreach (string rutaArchivo in archivosExportados)
    {
        using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
        });

        var dataTable = dataSet.Tables[0];
        
        var filasValidas = dataTable.AsEnumerable()
            .Where(row => row[1] != DBNull.Value && !string.IsNullOrEmpty(row[1].ToString()) &&
                          row[0] != DBNull.Value && row[0] is DateTime);

        var registrosAgrupados = filasValidas.GroupBy(row => new
            {
                IdUsuario = row[1].ToString(),
                Fecha = ((DateTime)row[0]).Date
            });

        foreach (var grupo in registrosAgrupados)
        {
            var marcacionesDelDia = grupo.OrderBy(row => (DateTime)row[0]).ToList();
            
            var marcacionesAlmuerzo = marcacionesDelDia
                .Where(m => ((DateTime)m[0]).TimeOfDay >= new TimeSpan(12, 30, 0) &&
                              ((DateTime)m[0]).TimeOfDay < new TimeSpan(15, 0, 0))
                .ToList();

            var marcacionesNoAlmuerzo = marcacionesDelDia.Except(marcacionesAlmuerzo).ToList();

            DataRow primeraMarcacionAlmuerzo = marcacionesAlmuerzo.FirstOrDefault();
            DataRow segundaMarcacionAlmuerzo = marcacionesAlmuerzo.Skip(1).FirstOrDefault();

            foreach (var currentRow in marcacionesDelDia)
            {
                var horaEvento = (DateTime)currentRow[0];
                var nombre = currentRow[2].ToString();
                var apellido = currentRow[3].ToString();
                var estado = currentRow[8].ToString();

                bool esSalidaAlmuerzo = (primeraMarcacionAlmuerzo != null && currentRow == primeraMarcacionAlmuerzo);
                bool esLlegadaAlmuerzo = (segundaMarcacionAlmuerzo != null && currentRow == segundaMarcacionAlmuerzo);

                bool esEntrada = false;
                bool esSalida = false;

                if (!esSalidaAlmuerzo && !esLlegadaAlmuerzo)
                {
                    if (currentRow == marcacionesNoAlmuerzo.FirstOrDefault())
                    {
                        esEntrada = true;
                    }
                    if (marcacionesNoAlmuerzo.Count > 1 && currentRow == marcacionesNoAlmuerzo.LastOrDefault())
                    {
                        esSalida = true;
                    }
                }

                var biometricoDataDto = new BiometricoDataDTO
                {
                    Nombre = nombre,
                    Apellido = apellido,
                    Hora = horaEvento,
                    Detalle = estado,
                    EsEntrada = esEntrada,
                    EsSalida = esSalida,
                    EsSalidaAlmuerzo = esSalidaAlmuerzo,
                    EsLlegadaAlmuerzo = esLlegadaAlmuerzo
                };

                var response = await httpClient.PostAsJsonAsync(apiUrl, biometricoDataDto);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"OK: Registro de {nombre} {apellido} a las {horaEvento} enviado.");
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"ERROR al enviar registro de {nombre} {apellido}: {response.StatusCode} - {errorBody}");
                }
            }
        }
        Console.WriteLine($"Archivo {Path.GetFileName(rutaArchivo)} procesado.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}");
}

Console.WriteLine("Procesamiento de archivos del biométrico finalizado.");
Console.ReadKey();