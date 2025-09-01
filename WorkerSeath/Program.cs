using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using ef_core.DTOs;

Console.WriteLine("WorkerSeat: leyendo archivo PDF...");

try
{
    string pdfPath = @"C:\Users\pguayas1\Documents\pruebas_worker_seat\18 DE AGOSTO AL 22 DE AGOSTO DE 2025.pdf";
    string apiUrl = "http://localhost:5165/api/SeatData";

    using var httpClient = new HttpClient();
    using var document = PdfDocument.Open(pdfPath);

    foreach (var page in document.GetPages())
    {
        string pageText = page.Text;

        // Detecta líneas
        var lineas = pageText.Split('\n');

        string? nombre = null;
        string? apellido = null;
        DateTime? horaEntrada = null;
        DateTime? horaSalidaAlmuerzo = null;
        DateTime? horaRegresoAlmuerzo = null;
        DateTime? horaSalida = null;

        foreach (var linea in lineas)
        {
            // Si detecta nombre/apellido
            if (linea.StartsWith("Empleado:"))
            {
                var partes = linea.Replace("Empleado:", "").Trim().Split(' ');
                nombre = partes[0];
                apellido = partes.Length > 1 ? partes[1] : null;
            }

            // Buscar fechas y horas en formato dd/MM/yyyy HH:mm
            var match = Regex.Match(linea, @"(\d{1,2}/\d{1,2}/\d{4})\s+(\d{1,2}:\d{2})");
            if (match.Success)
            {
                DateTime horaEvento = DateTime.ParseExact(
                    $"{match.Groups[1].Value} {match.Groups[2].Value}",
                    "d/M/yyyy HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture
                );

                if (linea.Contains("Entrada"))
                    horaEntrada = horaEvento;
                else if (linea.Contains("Salida Almuerzo"))
                    horaSalidaAlmuerzo = horaEvento;
                else if (linea.Contains("Regreso Almuerzo"))
                    horaRegresoAlmuerzo = horaEvento;
                else if (linea.Contains("Salida"))
                    horaSalida = horaEvento;
            }
        }

        // Si encontró datos, los envía a la API
        if (nombre != null)
        {
            var seatData = new SeatDataDTO
            {
                Nombre = nombre,
                Apellido = apellido,
                HoraEntrada = horaEntrada,
                HoraSalidaAlmuerzo = horaSalidaAlmuerzo,
                HoraRegresoAlmuerzo = horaRegresoAlmuerzo,
                HoraSalida = horaSalida,
                Detalle = "Importado desde PDF Seat"
            };

            var response = await httpClient.PostAsJsonAsync(apiUrl, seatData);
            if (response.IsSuccessStatusCode)
                Console.WriteLine($"Registro de {nombre} {apellido} enviado correctamente.");
            else
                Console.WriteLine($"Error al enviar {nombre}: {response.StatusCode}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error en WorkerSeat: {ex.Message}");
}
