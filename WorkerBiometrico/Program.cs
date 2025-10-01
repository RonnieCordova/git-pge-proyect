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

Console.WriteLine("WorkerBiometrico: Leyendo archivos de Excel...");
// Se registra el proveedor de codificación para compatibilidad con archivos Excel antiguos.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    // --- 1. CONFIGURACIÓN DE RUTAS Y BÚSQUEDA DE ARCHIVOS ---
    string carpetaReportes = @"COLOCAR RUTA";
    string carpetaArchivados = Path.Combine(carpetaReportes, "procesados");
    Directory.CreateDirectory(carpetaArchivados); // Crea la carpeta si no existe.

    // Se buscan todos los archivos con extensión .xlsx o .xls en la carpeta principal.
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

    // Se inicializa el cliente HTTP para comunicarse con la API.
    using var httpClient = new HttpClient();
    string apiUrl = "http://localhost:5165/api/BiometricoData";

    // --- 2. PROCESAMIENTO DE CADA ARCHIVO ENCONTRADO ---
    foreach (string rutaArchivo in archivosExportados)
    {
        DataTable dataTable;

        // --- Bloque de Lectura Aislado ---
        // Se lee todo el contenido del archivo Excel y se carga en una tabla en memoria.
        // El bloque 'using' garantiza que el archivo se cierre inmediatamente después de la lectura,
        // liberándolo para poder moverlo más tarde.
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
            continue; // Si un archivo no se puede leer, se salta al siguiente.
        }

        // --- 3. PROCESAMIENTO DE DATOS EN MEMORIA ---
        // A partir de este punto, se trabaja con 'dataTable', que es una copia de los datos.
        // El archivo original ya no está en uso.

        var filasValidas = dataTable.AsEnumerable()
            .Where(row => row[1] != DBNull.Value && !string.IsNullOrEmpty(row[1].ToString()) &&
                          row[0] != DBNull.Value && row[0] is DateTime);

        // Se agrupan todas las marcaciones por empleado y por día.
        var registrosAgrupados = filasValidas.GroupBy(row => new
        {
            IdUsuario = row[1].ToString(),
            Fecha = ((DateTime)row[0]).Date
        });

        foreach (var grupo in registrosAgrupados)
        {
            // Se ordenan las marcaciones del día cronológicamente.
            var marcacionesDelDia = grupo.OrderBy(row => (DateTime)row[0]).ToList();

            // Se identifica la lógica de las marcaciones de almuerzo.
            var marcacionesAlmuerzo = marcacionesDelDia
                .Where(m => ((DateTime)m[0]).TimeOfDay >= new TimeSpan(12, 30, 0) &&
                              ((DateTime)m[0]).TimeOfDay < new TimeSpan(15, 0, 0))
                .ToList();
            var marcacionesNoAlmuerzo = marcacionesDelDia.Except(marcacionesAlmuerzo).ToList();
            DataRow primeraMarcacionAlmuerzo = marcacionesAlmuerzo.FirstOrDefault();
            DataRow segundaMarcacionAlmuerzo = marcacionesAlmuerzo.Skip(1).FirstOrDefault();

            // Se itera sobre cada marcación para clasificarla y enviarla a la API.
            foreach (var currentRow in marcacionesDelDia)
            {
                var horaEvento = (DateTime)currentRow[0];
                var nombre = currentRow[2].ToString();
                var apellido = currentRow[3].ToString();
                var estado = currentRow[8].ToString();

                // Se determina el tipo de marcación (Entrada, Salida, Almuerzo).
                bool esSalidaAlmuerzo = (primeraMarcacionAlmuerzo != null && currentRow == primeraMarcacionAlmuerzo);
                bool esLlegadaAlmuerzo = (segundaMarcacionAlmuerzo != null && currentRow == segundaMarcacionAlmuerzo);
                bool esEntrada = false;
                bool esSalida = false;

                if (!esSalidaAlmuerzo && !esLlegadaAlmuerzo)
                {
                    if (currentRow == marcacionesNoAlmuerzo.FirstOrDefault())
                    {
                        esEntrada = true;
                    }
                    if (marcacionesNoAlmuerzo.Count > 1 && currentRow == marcacionesNoAlmuerzo.LastOrDefault())
                    {
                        esSalida = true;
                    }
                }

                // Se construye el objeto DTO para enviar a la API.
                var biometricoDataDto = new BiometricoDataDTO
                {
                    Nombre = nombre,
                    Apellido = apellido,
                    Hora = horaEvento,
                    Detalle = estado,
                    EsEntrada = esEntrada,
                    EsSalida = esSalida,
                    EsSalidaAlmuerzo = esSalidaAlmuerzo,
                    EsLlegadaAlmuerzo = esLlegadaAlmuerzo
                };

                // Se envía el registro a la API.
                var response = await httpClient.PostAsJsonAsync(apiUrl, biometricoDataDto);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"ERROR al enviar registro de {nombre} {apellido}: {response.StatusCode} - {errorBody}");
                }
            }
        }
        Console.WriteLine($"Archivo {Path.GetFileName(rutaArchivo)} procesado.");

        // --- 4. MOVER EL ARCHIVO PROCESADO ---
        // Como el archivo fue cerrado en el paso 2, ahora se puede mover sin problemas.
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

Console.WriteLine("Procesamiento de archivos del biométrico finalizado.");
Console.ReadKey();