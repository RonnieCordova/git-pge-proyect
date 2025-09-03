using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ef_core.DTOs; // Asegúrate de que tu worker tenga referencia a este proyecto

Console.WriteLine("WorkerSeat (Versión Definitiva por Zonas): Iniciando lectura de PDF...");

try
{
    string pdfPath = @"C:\Users\pguayas1\Documents\pruebas_worker_seat\18 DE AGOSTO AL 22 DE AGOSTO DE 2025.pdf"; // Asegúrate que esta ruta sea correcta
    string apiUrl = "http://localhost:5165/api/SeatData"; // Asegúrate que el puerto sea el correcto

    using var httpClient = new HttpClient();
    using var document = PdfDocument.Open(pdfPath);

    var nameRegex = new Regex(@"^([A-ZÑÁÉÍÓÚ]{2,}(?:\s[A-ZÑÁÉÍÓÚ]{2,})+$)");
    var dateRegex = new Regex(@"^\d{2}/\d{2}/\d{4}$");
    var timeRegex = new Regex(@"^\d{2}:\d{2}$");

    foreach (var page in document.GetPages())
    {
        var words = page.GetWords();
        var employeeBlocks = new List<EmployeeBlock>();
        var processedLines = new HashSet<double>();

        // 1. IDENTIFICAR TODOS LOS BLOQUES DE EMPLEADOS POR SU POSICIÓN Y
        foreach (var word in words)
        {
            double lineY = Math.Round(word.BoundingBox.Centroid.Y, 2);
            if (processedLines.Contains(lineY)) continue;

            var lineWords = words.Where(w => Math.Abs(w.BoundingBox.Centroid.Y - lineY) < 2)
                                 .OrderBy(w => w.BoundingBox.Left);
            string potentialName = string.Join(" ", lineWords.Select(w => w.Text));

            if (nameRegex.IsMatch(potentialName) && !IsCommonHeader(potentialName))
            {
                employeeBlocks.Add(new EmployeeBlock { FullName = potentialName, Y_Position = lineY });
                processedLines.Add(lineY);
            }
        }
        
        employeeBlocks = employeeBlocks.OrderBy(b => b.Y_Position).ToList();
        Console.WriteLine($"\n--- Página {page.Number}: Se encontraron {employeeBlocks.Count} bloques de empleados. ---");

        // 2. PROCESAR CADA BLOQUE (ZONA) DE EMPLEADO
        for (int i = 0; i < employeeBlocks.Count; i++)
        {
            var currentBlock = employeeBlocks[i];
            double startY = currentBlock.Y_Position;
            // La zona termina donde empieza el siguiente empleado, o al final de la página
            double endY = (i + 1 < employeeBlocks.Count) ? employeeBlocks[i + 1].Y_Position : page.Height;

            var dateWordsInBlock = words
                .Where(w => dateRegex.IsMatch(w.Text) && w.BoundingBox.Centroid.Y > startY && w.BoundingBox.Centroid.Y < endY)
                .ToList();

            foreach (var dateWord in dateWordsInBlock)
            {
                string dateStr = dateWord.Text;
                var wordsInLine = words.Where(w => Math.Abs(w.BoundingBox.Centroid.Y - dateWord.BoundingBox.Centroid.Y) < 2).OrderBy(w => w.BoundingBox.Left).ToList();
                var timeAndDetailWords = wordsInLine.Skip(1).Select(w => w.Text).ToList();
                var times = timeAndDetailWords.Where(w => timeRegex.IsMatch(w)).ToList();
                var details = timeAndDetailWords.Where(w => !timeRegex.IsMatch(w) && !new[] { "P", "T", "M" }.Contains(w)).ToList();

                var nameParts = currentBlock.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string nombre = "";
                string apellido = "";

                if (nameParts.Length >= 4) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = $"{nameParts[2]} {nameParts[3]}"; }
                else if (nameParts.Length == 3) { apellido = $"{nameParts[0]} {nameParts[1]}"; nombre = nameParts[2]; }
                else if (nameParts.Length == 2) { apellido = nameParts[0]; nombre = nameParts[1]; }
                else { apellido = currentBlock.FullName; }
                
                var seatData = new SeatDataDTO
                {
                    Nombre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombre.ToLower()),
                    Apellido = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(apellido.ToLower()),
                    Detalle = $"Importado desde PDF - {dateStr}"
                };

                if (times.Count > 0) seatData.HoraEntrada = ParseDateTime(dateStr, times[0]);
                if (times.Count == 2) seatData.HoraSalida = ParseDateTime(dateStr, times[1]);
                if (times.Count == 3) { seatData.HoraSalidaAlmuerzo = ParseDateTime(dateStr, times[1]); seatData.HoraSalida = ParseDateTime(dateStr, times[2]); }
                if (times.Count >= 4) { seatData.HoraSalidaAlmuerzo = ParseDateTime(dateStr, times[1]); seatData.HoraRegresoAlmuerzo = ParseDateTime(dateStr, times[2]); seatData.HoraSalida = ParseDateTime(dateStr, times[3]); }
                if (details.Any()) seatData.Detalle += " - " + string.Join(" ", details);
                
                var response = await httpClient.PostAsJsonAsync(apiUrl, seatData);
                if(response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"OK: Registro de {seatData.Apellido}, {seatData.Nombre} para el {dateStr} enviado.");
                } else {
                    Console.WriteLine($"ERROR al enviar registro de {seatData.Apellido}, {seatData.Nombre} para el {dateStr}.");
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR FATAL en WorkerSeat: {ex.Message}\n{ex.StackTrace}");
}

Console.WriteLine("\nProceso de lectura de PDF finalizado.");
Console.ReadKey();


// --- Funciones Auxiliares ---

DateTime? ParseDateTime(string date, string time)
{
    if (DateTime.TryParseExact($"{date} {time}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
    {
        return result;
    }
    return null;
}

bool IsCommonHeader(string text)
{
    var headers = new List<string> { "PROCURADURÍA", "GENERAL DEL ESTADO", "REPUBLICA DEL ECUADOR", "ASISTENCIA POR PROCESO", "DIRECCIÓN REGIONAL", "SECRETARÍA REGIONAL", "Tipo Permiso", "lunch", "Fecha", "Entrada", "Saluda", "Salida", "Modalidad" };
    return headers.Any(h => text.ToUpper().Contains(h));
}

public class EmployeeBlock
{
    public string FullName { get; set; } = "";
    public double Y_Position { get; set; }
}