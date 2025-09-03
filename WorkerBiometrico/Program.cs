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

        foreach (DataRow currentRow in dataTable.Rows)
        {
            // --- Verificación de Fila Válida ---
            // Si la columna de ID (1) o Tiempo (0) está vacía o no es una fecha, la ignoramos.
            if (currentRow[1] == DBNull.Value || string.IsNullOrEmpty(currentRow[1].ToString()) ||
                currentRow[0] == DBNull.Value || !(currentRow[0] is DateTime))
            {
                continue; // Salta a la siguiente fila
            }

            var horaEvento = (DateTime)currentRow[0];
            var nombre = currentRow[2].ToString();
            var apellido = currentRow[3].ToString();
            var estado = currentRow[8].ToString(); // "Entrada" o "Salida"

            // --- Lógica Simplificada: El worker solo reporta lo que ve ---
            var biometricoDataDto = new BiometricoDataDTO
            {
                Nombre = nombre,
                Apellido = apellido,
                Hora = horaEvento,
                Detalle = estado, // Guardamos el estado crudo ("Entrada" o "Salida")
                EsEntrada = estado.ToLower().Contains("entrada"),
                EsSalida = estado.ToLower().Contains("salida"),
                EsSalidaAlmuerzo = false, // La interpretación se hará en el UnificationService
                EsLlegadaAlmuerzo = false  // La interpretación se hará en el UnificationService
            };

            var response = await httpClient.PostAsJsonAsync(apiUrl, biometricoDataDto);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR al enviar registro de {nombre} {apellido}: {response.StatusCode} - {errorBody}");
            }
        }
        Console.WriteLine($"Archivo {Path.GetFileName(rutaArchivo)} procesado y datos enviados.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}");
}

Console.WriteLine("Procesamiento de archivos del biométrico finalizado.");
Console.ReadKey();