using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ExcelDataReader;
using System.Text;
using System.Linq;
using ef_core.DTOs;
using ef_core.Data;
using System.Collections.Generic;

Console.WriteLine("WorkerBiometrico: Leyendo archivos de Excel...");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_biometrico";
    string[] archivosExportados = Directory.GetFiles(carpetaReportes, "*.xlsx")
                                         .Union(Directory.GetFiles(carpetaReportes, "*.xls"))
                                         .ToArray();

    if (archivosExportados.Length == 0)
    {
        Console.WriteLine("No se encontraron archivos de Excel para procesar.");
        return;
    }

    Console.WriteLine($"Se encontraron {archivosExportados.Length} archivos. Procesando...");

    using var httpClient = new HttpClient();
    string apiUrl = "http://localhost:5165/api/BiometricoData";

    foreach (string rutaArchivo in archivosExportados)
    {
        using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
        });

        var dataTable = dataSet.Tables[0];
        
        // Filtramos las filas ANTES de agrupar para asegurar que los datos son válidos
        var filasValidas = dataTable.AsEnumerable()
            .Where(row => row[1] != DBNull.Value && !string.IsNullOrEmpty(row[1].ToString()) && // Tiene ID de Usuario
                          row[0] != DBNull.Value && double.TryParse(row[0].ToString(), out _));  // La columna Tiempo es un número

        var registrosAgrupados = filasValidas.GroupBy(row => new
            {
                IdUsuario = row[1].ToString(),
                Fecha = DateTime.FromOADate(Convert.ToDouble(row[0])).Date
            });

        foreach (var grupo in registrosAgrupados)
        {
            var marcacionesDelDia = grupo.OrderBy(row => Convert.ToDouble(row[0])).ToList();
            var marcacionesAlmuerzo = marcacionesDelDia
                .Where(m => DateTime.FromOADate(Convert.ToDouble(m[0])).TimeOfDay >= new TimeSpan(12, 0, 0) &&
                              DateTime.FromOADate(Convert.ToDouble(m[0])).TimeOfDay < new TimeSpan(15, 0, 0))
                .ToList();

            DataRow primeraMarcacionAlmuerzo = marcacionesAlmuerzo.FirstOrDefault();
            DataRow segundaMarcacionAlmuerzo = marcacionesAlmuerzo.Skip(1).FirstOrDefault();

            foreach (var currentRow in marcacionesDelDia)
            {
                var horaEvento = DateTime.FromOADate(Convert.ToDouble(currentRow[0]));
                var nombre = currentRow[2].ToString();
                var apellido = currentRow[3].ToString();
                var estado = currentRow[8].ToString();

                bool esSalidaAlmuerzo = (primeraMarcacionAlmuerzo != null && currentRow == primeraMarcacionAlmuerzo);
                bool esLlegadaAlmuerzo = (segundaMarcacionAlmuerzo != null && currentRow == segundaMarcacionAlmuerzo);

                var biometricoDataDto = new BiometricoDataDTO
                {
                    Nombre = nombre,
                    Apellido = apellido,
                    Hora = horaEvento,
                    Detalle = estado,
                    EsEntrada = !esSalidaAlmuerzo && !esLlegadaAlmuerzo && currentRow == marcacionesDelDia.First(),
                    EsSalida = !esSalidaAlmuerzo && !esLlegadaAlmuerzo && currentRow == marcacionesDelDia.Last(),
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