using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using ExcelDataReader;
using System.Text;
using ef_core.DTOs;
using System.Linq;
using System.Globalization;

Console.WriteLine("WorkerBiometrico: Leyendo archivos de Excel...");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_biometrico";

    // Busca y combina archivos con extensión .xlsx y .xls
    string[] archivosExportados = Directory.GetFiles(carpetaReportes, "*.xlsx")
                                         .Union(Directory.GetFiles(carpetaReportes, "*.xls"))
                                         .ToArray();

    if (archivosExportados.Length > 0)
    {
        Console.WriteLine($"Se encontraron {archivosExportados.Length} archivos. Procesando...");

        using (var httpClient = new HttpClient())
        {
            string apiUrl = "http://localhost:5165/api/BiometricoData";

            foreach (string rutaArchivo in archivosExportados)
            {
                using (var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true
                            }
                        });

                        var dataTable = result.Tables[0];
                        for (int row = 0; row < dataTable.Rows.Count; row++)
                        {
                            var currentRow = dataTable.Rows[row];

                            // Lectura de las columnas exactas que necesitas
                            var nombre = currentRow["Nombre"].ToString();
                            var apellido = currentRow["Apellido"].ToString();
                            var estado = currentRow["Estado"].ToString();

                            var horaEvento = DateTime.ParseExact(
                                currentRow["Tiempo"].ToString(),
                                "d/M/yyyy H:mm:ss",
                                CultureInfo.InvariantCulture
                            );

                            // Lógica para clasificar el tipo de evento
                            bool esSalidaAlmuerzo = false;
                            bool esLlegadaAlmuerzo = false;
                            if (horaEvento.TimeOfDay >= new TimeSpan(12, 30, 0) &&
                                horaEvento.TimeOfDay <= new TimeSpan(15, 0, 0))
                            {
                                if (estado.ToLower().Contains("salida"))
                                {
                                    esSalidaAlmuerzo = true;
                                }
                                else if (estado.ToLower().Contains("entrada") || estado.ToLower().Contains("regreso"))
                                {
                                    esLlegadaAlmuerzo = true;
                                }
                            }

                            var biometricoDataDto = new BiometricoDataDTO
                            {
                                Nombre = nombre,
                                Apellido = apellido,
                                Hora = horaEvento,
                                Detalle = estado,
                                EsEntrada = estado.ToLower().Contains("entrada") && !esLlegadaAlmuerzo,
                                EsSalida = estado.ToLower().Contains("salida") && !esSalidaAlmuerzo,
                                EsSalidaAlmuerzo = esSalidaAlmuerzo,
                                EsLlegadaAlmuerzo = esLlegadaAlmuerzo
                            };

                            var response = await httpClient.PostAsJsonAsync(apiUrl, biometricoDataDto);
                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Registro de {nombre} {apellido} enviado correctamente.");
                            }
                            else
                            {
                                string errorBody = await response.Content.ReadAsStringAsync();
                                Console.WriteLine($"Error al enviar registro de {nombre} {apellido}: {response.StatusCode} - {errorBody}");
                            }
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
    Console.WriteLine($"Ocurrió un error inesperado: {ex.Message}");
}

Console.WriteLine("Procesamiento de archivos del biométrico finalizado.");
Console.ReadKey();