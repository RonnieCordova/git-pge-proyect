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

Console.WriteLine("WorkerSeat (Versión Excel): Iniciando lectura de archivo...");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    // Asegúrate de que esta ruta apunte a la carpeta con tu nuevo archivo Excel
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_seat"; 
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
    string apiUrl = "http://localhost:5165/api/SeatData"; // Asegúrate que el puerto sea el correcto

    foreach (string rutaArchivo in archivosExportados)
    {
        using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true } // La primera fila es la cabecera
        });
        
        var dataTable = dataSet.Tables[0];

        foreach (DataRow row in dataTable.Rows)
        {
            // Validar que la fila tiene los datos mínimos necesarios
            if (row[0] == DBNull.Value || row[1] == DBNull.Value || !(row[1] is DateTime))
            {
                continue; // Si no hay nombre de empleado o fecha, se ignora la fila
            }

            string empleadoActual = row[0].ToString() ?? "";
            DateTime fecha = (DateTime)row[1];

            var nameParts = empleadoActual.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string nombre = "";
            string apellido = "";

            if (nameParts.Length >= 4) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = $"{nameParts[2]} {nameParts[3]}"; }
            else if (nameParts.Length == 3) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = nameParts[2]; }
            else if (nameParts.Length == 2) { apellido = nameParts[0]; nombre = nameParts[1]; }
            else { apellido = empleadoActual; }

            var seatData = new SeatDataDTO
            {
                Nombre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombre.ToLower()),
                Apellido = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(apellido.ToLower()),
                Detalle = $"Importado desde Excel - {fecha:dd/MM/yyyy}"
            };

            // Extraer y combinar fecha con horas
            seatData.HoraEntrada = CombineDateAndTime(fecha, row[2]);        // Columna "Entrada"
            seatData.HoraSalidaAlmuerzo = CombineDateAndTime(fecha, row[3]);  // Columna "Salida a Almorzar"
            seatData.HoraRegresoAlmuerzo = CombineDateAndTime(fecha, row[4]); // Columna "Entrada de Almorzar"
            seatData.HoraSalida = CombineDateAndTime(fecha, row[5]);          // Columna "Salida"

            var response = await httpClient.PostAsJsonAsync(apiUrl, seatData);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"OK: Registro de {seatData.Apellido}, {seatData.Nombre} para el {fecha:dd/MM/yyyy} enviado.");
            }
            else
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ERROR al enviar registro de {seatData.Apellido}, {seatData.Nombre}: {response.StatusCode} - {errorBody}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}");
}

Console.WriteLine("\nProcesamiento de archivos del SEAT finalizado.");
Console.ReadKey();


// --- Funciones Auxiliares ---

DateTime? CombineDateAndTime(DateTime date, object timeObject)
{
    if (timeObject == DBNull.Value || timeObject == null) return null;

    // ExcelDataReader a menudo lee las horas como DateTime (con fecha 1899) o como TimeSpan
    if (timeObject is DateTime time)
    {
        return date.Date + time.TimeOfDay;
    }
    if (timeObject is TimeSpan timeSpan)
    {
        return date.Date + timeSpan;
    }
    // Intenta parsear si es un string
    if (TimeSpan.TryParse(timeObject.ToString(), out var parsedTimeSpan))
    {
        return date.Date + parsedTimeSpan;
    }

    return null;
}