using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using ef_core.DTOs;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Collections.Generic;

Console.WriteLine("WorkerSeath: Leyendo archivos de PDF...");

try
{
    string carpetaReportes = @"C:\Users\pguayas1\Documents\pruebas_worker_seat";
    string[] archivosPdf = Directory.GetFiles(carpetaReportes, "*.pdf");

    if (archivosPdf.Length > 0)
    {
        Console.WriteLine($"Se encontraron {archivosPdf.Length} archivos. Procesando...");

        using (var httpClient = new HttpClient())
        {
            string apiUrl = "http://localhost:5165/api/SeatData";

            foreach (string rutaArchivo in archivosPdf)
            {
                string pdfTexto = ExtraerTextoDePdf(rutaArchivo);
                var registros = ParsearRegistrosDeSeath(pdfTexto);

                foreach (var registro in registros)
                {
                    var response = await httpClient.PostAsJsonAsync(apiUrl, registro);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✅ Registro de {registro.Nombre} {registro.Apellido} para el {registro.HoraEntrada?.ToShortDateString() ?? "N/A"} enviado correctamente.");
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ Error al enviar registro de {registro.Nombre} {registro.Apellido}: {response.StatusCode} - {errorBody}");
                    }
                }
            }
        }
    }
    else
    {
        Console.WriteLine("No se encontraron archivos PDF para procesar.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Ocurrió un error inesperado: {ex.Message}");
}

Console.WriteLine("Procesamiento de archivos de SEATH finalizado.");
Console.ReadKey();

// --- Funciones de Ayuda ---

static string ExtraerTextoDePdf(string ruta)
{
    var stringBuilder = new StringBuilder();
    using (var pdfDocument = new PdfDocument(new PdfReader(ruta)))
    {
        var numberOfPages = pdfDocument.GetNumberOfPages();
        for (int i = 1; i <= numberOfPages; i++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            var page = pdfDocument.GetPage(i);
            stringBuilder.Append(PdfTextExtractor.GetTextFromPage(page, strategy));
        }
    }
    return stringBuilder.ToString();
}

static List<SeatDataDTO> ParsearRegistrosDeSeath(string contenido)
{
    var registros = new List<SeatDataDTO>();
    var lineas = contenido.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    string nombreActual = null;
    string apellidoActual = null;
    
    foreach (var linea in lineas)
    {
        // 1. Busca el nombre en la línea actual
        var matchNombre = Regex.Match(linea.Trim(), @"^([A-ZÁÉÍÓÚ\s]+)\s+([A-ZÁÉÍÓÚ\s]+)$");
        if (matchNombre.Success)
        {
            nombreActual = matchNombre.Groups[1].Value.Trim();
            apellidoActual = matchNombre.Groups[2].Value.Trim();
            continue;
        }

        // 2. Si la línea contiene una fecha y ya tenemos un nombre, procesa el registro
        var matchRegistro = Regex.Match(linea, @"(\d{2}/\d{2}/\d{4})(.+)");
        if (!string.IsNullOrEmpty(nombreActual) && matchRegistro.Success)
        {
            var fecha = DateTime.ParseExact(matchRegistro.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            var datosRaw = matchRegistro.Groups[2].Value.Trim().Replace("\"", "");
            
            // Reemplaza múltiples comas por una sola y quita espacios
            datosRaw = Regex.Replace(datosRaw, @",\s*,\s*", ",");
            var campos = datosRaw.Split(',');

            var registro = new SeatDataDTO
            {
                Nombre = nombreActual,
                Apellido = apellidoActual
            };

            TimeSpan tempTime;
            
            int index = 0;
            
            // Hora de Entrada
            if (campos.Length > index && TimeSpan.TryParse(campos[index].Trim(), out tempTime))
            {
                registro.HoraEntrada = fecha.Add(tempTime);
            }
            else if (campos.Length > index && !string.IsNullOrEmpty(campos[index].Trim()))
            {
                registro.Detalle = campos[index].Trim();
            }
            index++;
            
            // Hora de Salida Almuerzo
            if (campos.Length > index && TimeSpan.TryParse(campos[index].Trim(), out tempTime))
            {
                registro.HoraSalidaAlmuerzo = fecha.Add(tempTime);
            }
            index++;
            
            // Hora de Regreso Almuerzo
            if (campos.Length > index && TimeSpan.TryParse(campos[index].Trim(), out tempTime))
            {
                registro.HoraRegresoAlmuerzo = fecha.Add(tempTime);
            }
            index++;
            
            // Hora de Salida
            if (campos.Length > index && TimeSpan.TryParse(campos[index].Trim(), out tempTime))
            {
                registro.HoraSalida = fecha.Add(tempTime);
            }
            index++;
            
            // Detalle (si no se capturó antes)
            if (campos.Length > index && !string.IsNullOrEmpty(campos[index].Trim()))
            {
                if (string.IsNullOrEmpty(registro.Detalle))
                {
                    registro.Detalle = campos[index].Trim();
                }
            }

            registros.Add(registro);
        }
        else if (linea.Trim().StartsWith("Pagina") || linea.Trim().StartsWith("Proceso:"))
        {
            // Reinicia las variables de nombre al encontrar una nueva sección
            nombreActual = null;
            apellidoActual = null;
        }
    }
    return registros;
}