// Worker Definitivo v3 - Lógica de búsqueda inversa (Look-behind)
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
using ef_core.DTOs;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WorkerSeat: Iniciando lectura de archivo PDF...");

        try
        {
            string pdfPath = @"C:\Users\pguayas1\Documents\pruebas_worker_seat\18 DE AGOSTO AL 22 DE AGOSTO DE 2025.pdf";
            string apiUrl = "http://localhost:5165/api/SeatData";

            using var httpClient = new HttpClient();
            using var document = PdfDocument.Open(pdfPath);

            var nameRegex = new Regex(@"^([A-ZÑÁÉÍÓÚ]{2,}(?:\s[A-ZÑÁÉÍÓÚ]{2,})+$)");
            var dateRegex = new Regex(@"^\d{2}/\d{2}/\d{4}$");
            var timeRegex = new Regex(@"^\d{2}:\d{2}$");

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords();

                // Buscamos todas las palabras que son fechas para usarlas como anclas
                var dateWords = words.Where(w => dateRegex.IsMatch(w.Text)).ToList();

                foreach (var dateWord in dateWords)
                {
                    // LÓGICA PRINCIPAL: Por cada fecha, buscamos hacia arriba para encontrar el nombre
                    var nameWords = words
                        .Where(w => w.BoundingBox.Bottom < dateWord.BoundingBox.Top && Math.Abs(w.BoundingBox.Centroid.X - dateWord.BoundingBox.Centroid.X) < 100)
                        .OrderByDescending(w => w.BoundingBox.Top)
                        .ToList();

                    string employeeName = "Nombre No Encontrado";
                    foreach (var potentialNameWord in nameWords)
                    {
                        var lineWords = words.Where(w => Math.Abs(w.BoundingBox.Centroid.Y - potentialNameWord.BoundingBox.Centroid.Y) < 2)
                                             .OrderBy(w => w.BoundingBox.Left)
                                             .Select(w => w.Text);
                        string potentialName = string.Join(" ", lineWords);

                        if (nameRegex.IsMatch(potentialName) && !IsCommonHeader(potentialName))
                        {
                            employeeName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(potentialName.ToLower());
                            break; // Encontramos el nombre, salimos del bucle
                        }
                    }

                    // Ahora procesamos la línea de la fecha encontrada
                    string dateStr = dateWord.Text;
                    var wordsInLine = words.Where(w => Math.Abs(w.BoundingBox.Centroid.Y - dateWord.BoundingBox.Centroid.Y) < 2).OrderBy(w => w.BoundingBox.Left).ToList();
                    var timeAndDetailWords = wordsInLine.Skip(1).Select(w => w.Text).ToList();

                    var times = timeAndDetailWords.Where(w => timeRegex.IsMatch(w)).ToList();
                    var details = timeAndDetailWords.Where(w => !timeRegex.IsMatch(w) && !new[] { "P", "T", "M" }.Contains(w)).ToList();

                    var nameParts = employeeName.Split(new[] { ' ' }, 2);
                    string nombre = nameParts.Length > 0 ? nameParts[0] : "";
                    string apellido = nameParts.Length > 1 ? nameParts[1] : "";

                    var seatData = new SeatDataDTO
                    {
                        Nombre = nombre,
                        Apellido = apellido,
                        Detalle = $"Importado desde PDF - {dateStr}"
                    };

                    if (times.Count > 0) seatData.HoraEntrada = ParseDateTime(dateStr, times[0]);
                    if (times.Count == 2) seatData.HoraSalida = ParseDateTime(dateStr, times[1]);
                    if (times.Count == 3)
                    {
                        seatData.HoraSalidaAlmuerzo = ParseDateTime(dateStr, times[1]);
                        seatData.HoraSalida = ParseDateTime(dateStr, times[2]);
                    }
                    if (times.Count >= 4)
                    {
                        seatData.HoraSalidaAlmuerzo = ParseDateTime(dateStr, times[1]);
                        seatData.HoraRegresoAlmuerzo = ParseDateTime(dateStr, times[2]);
                        seatData.HoraSalida = ParseDateTime(dateStr, times[3]);
                    }
                    if (details.Any()) seatData.Detalle += " - " + string.Join(" ", details);

                    Console.WriteLine($"Empleado: {employeeName} | Fecha: {dateStr} | Marcaciones: [{string.Join(", ", times)}] | Detalles: [{string.Join(", ", details)}]");

                    // DESCOMENTA LA LÍNEA DE ABAJO PARA ENVIAR LOS DATOS A TU API
                    await httpClient.PostAsJsonAsync(apiUrl, seatData);
                }
            }
            Console.WriteLine("\nProceso de lectura de PDF finalizado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR FATAL en WorkerSeat: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static DateTime? ParseDateTime(string date, string time)
    {
        if (DateTime.TryParseExact($"{date} {time}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }
        return null;
    }

    static bool IsCommonHeader(string text)
    {
        var headers = new List<string> { "PROCURADURÍA", "GENERAL DEL ESTADO", "REPUBLICA DEL ECUADOR", "ASISTENCIA POR PROCESO", "DIRECCIÓN REGIONAL", "SECRETARÍA REGIONAL", "Tipo Permiso", "lunch", "Fecha", "Entrada", "Saluda", "Salida", "Modalidad" };
        return headers.Any(h => text.ToUpper().Contains(h));
    }
}