using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ExcelDataReader;
using ef_core.DTOs;

Console.WriteLine("WorkerSeat: Iniciando lectura de archivo...");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    string carpetaReportes = @"COLOCAR RUTA";
    string carpetaArchivados = Path.Combine(carpetaReportes, "procesados");
    Directory.CreateDirectory(carpetaArchivados);

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
    string apiUrl = "http://localhost:5165/api/SeatData";

    foreach (string rutaArchivo in archivosExportados)
    {
        DataTable dataTable;

        try
        {
            using (var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });
                dataTable = dataSet.Tables[0];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR al leer el archivo {Path.GetFileName(rutaArchivo)}: {ex.Message}");
            continue;
        }

        foreach (DataRow row in dataTable.Rows)
        {
            // Se valida que las celdas de nombre y fecha no estén vacías.
            if (row[1] == DBNull.Value || row[2] == DBNull.Value || string.IsNullOrEmpty(row[2].ToString()))
            {
                continue;
            }

            // Se intenta convertir la celda de la fecha a un objeto DateTime.
            // Si no se puede, se ignora la fila y se pasa a la siguiente.
            if (!DateTime.TryParse(row[2].ToString(), out DateTime fecha))
            {
                continue;
            }
        
            string area = row[0].ToString() ?? "Sin Área";
            string empleadoActual = row[1].ToString() ?? "";
            string tipoPermiso = row[7].ToString() ?? "Jornada normal";

            var nameParts = empleadoActual.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string nombre = "";
            string apellido = "";

            if (nameParts.Length >= 4) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = $"{nameParts[2]} {nameParts[3]}"; }
            else if (nameParts.Length == 3) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = nameParts[2]; }
            else if (nameParts.Length == 2) { apellido = nameParts[0]; nombre = nameParts[1]; }
            else { apellido = empleadoActual; }

            var seatData = new SeatDataDTO
            {
                Area = area,
                Nombre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombre.ToLower()),
                Apellido = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(apellido.ToLower()),
                Detalle = $"Importado desde Excel - {fecha:dd/MM/yyyy}",
                TipoPermiso = tipoPermiso
            };

            seatData.HoraEntrada = CombineDateAndTime(fecha, row[3]);
            seatData.HoraSalidaAlmuerzo = CombineDateAndTime(fecha, row[4]);
            seatData.HoraRegresoAlmuerzo = CombineDateAndTime(fecha, row[5]);
            seatData.HoraSalida = CombineDateAndTime(fecha, row[6]);

            var response = await httpClient.PostAsJsonAsync(apiUrl, seatData);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR al enviar registro de {seatData.Apellido}, {seatData.Nombre}: {response.StatusCode} - {errorBody}");
            }
        }
        
        Console.WriteLine($"Archivo {Path.GetFileName(rutaArchivo)} procesado.");

        try
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);
            string rutaDestino = Path.Combine(carpetaArchivados, nombreArchivo);
            File.Move(rutaArchivo, rutaDestino);
            Console.WriteLine($"--> Archivo '{nombreArchivo}' movido a la carpeta de procesados.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: No se pudo mover el archivo {Path.GetFileName(rutaArchivo)}. Detalles: {ex.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}");
}

Console.WriteLine("\nProcesamiento de archivos del SEAT finalizado.");
Console.ReadKey();


DateTime? CombineDateAndTime(DateTime date, object timeObject)
{
    if (timeObject == DBNull.Value || timeObject == null) return null;
    DateTime combined;

    if (timeObject is DateTime time)
    {
        combined = date.Date + time.TimeOfDay;
    }
    else if (timeObject is TimeSpan timeSpan)
    {
        combined = date.Date + timeSpan;
    }
    else if (TimeSpan.TryParse(timeObject.ToString(), out var parsedTimeSpan))
    {
        combined = date.Date + parsedTimeSpan;
    }
    else
    {
        return null;
    }
    
    return TimeZoneInfo.ConvertTimeToUtc(combined);
}