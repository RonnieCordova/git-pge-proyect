// Worker Definitivo v3.1 - Lógica de separación de nombres corregida
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
using ef_core.DTOs; // Asegúrate de tener la referencia a tus DTOs

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WorkerSeat: Iniciando lectura de archivo PDF...");

        try
        {
            // Asegúrate de que esta ruta sea la correcta en tu máquina.
            string pdfPath = @"C:\Users\LENOVO.USER\Documents\reportes_seat\18 DE AGOSTO AL 22 DE AGOSTO DE 2025.pdf";
            string apiUrl = "http://localhost:5165/api/SeatData";

            using var httpClient = new HttpClient();
            using var document = PdfDocument.Open(pdfPath);

            var nameRegex = new Regex(@"^([A-ZÑÁÉÍÓÚ]{2,}(?:\s[A-ZÑÁÉÍÓÚ]{2,})+$)");
            var dateRegex = new Regex(@"^\d{2}/\d{2}/\d{4}$");
            var timeRegex = new Regex(@"^\d{2}:\d{2}$");

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords();
                var dateWords = words.Where(w => dateRegex.IsMatch(w.Text)).ToList();

                foreach (var dateWord in dateWords)
                {
                    var nameWords = words
                        .Where(w => w.BoundingBox.Bottom < dateWord.BoundingBox.Top && Math.Abs(w.BoundingBox.Centroid.X - dateWord.BoundingBox.Centroid.X) < 150) // Aumentado el rango por si acaso
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
                            employeeName = potentialName; // Guardamos en mayúsculas para procesar
                            break;
                        }
                    }

                    string dateStr = dateWord.Text;
                    var wordsInLine = words.Where(w => Math.Abs(w.BoundingBox.Centroid.Y - dateWord.BoundingBox.Centroid.Y) < 2).OrderBy(w => w.BoundingBox.Left).ToList();
                    var timeAndDetailWords = wordsInLine.Skip(1).Select(w => w.Text).ToList();

                    var times = timeAndDetailWords.Where(w => timeRegex.IsMatch(w)).ToList();
                    var details = timeAndDetailWords.Where(w => !timeRegex.IsMatch(w) && !new[] { "P", "T", "M" }.Contains(w)).ToList();

                    // --- INICIO DE LA CORRECCIÓN DE NOMBRES ---
                    var nameParts = employeeName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string nombre = "";
                    string apellido = "";

                    if (nameParts.Length >= 4) // Ej: FLORES PARRA TATIANA FERNANDA
                    {
                        apellido = $"{nameParts[0]} {nameParts[1]}";
                        nombre = $"{nameParts[2]} {nameParts[3]}";
                    }
                    else if (nameParts.Length == 3) // Ej: YCAZA ABAD RAFAELLA
                    {
                        apellido = $"{nameParts[0]} {nameParts[1]}";
                        nombre = nameParts[2];
                    }
                    else if (nameParts.Length == 2) // Ej: JOSÉ NEIRA
                    {
                        apellido = nameParts[0];
                        nombre = nameParts[1];
                    }
                    else // Caso por defecto
                    {
                        apellido = employeeName;
                    }
                    
                    // Convertimos a TitleCase después de la separación
                    nombre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nombre.ToLower());
                    apellido = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(apellido.ToLower());
                    // --- FIN DE LA CORRECCIÓN DE NOMBRES ---

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
                    
                    Console.WriteLine($"Empleado: {apellido}, {nombre} | Fecha: {dateStr} | Marcaciones: [{string.Join(", ", times)}]");

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