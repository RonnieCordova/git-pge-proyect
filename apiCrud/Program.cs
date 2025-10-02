using Microsoft.EntityFrameworkCore;
using ef_core.Data;
using ef_core.Services;
using OfficeOpenXml;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    // Esta es la configuración más robusta: le dice a la app
    // que su contenido web SIEMPRE está en una carpeta 'wwwroot'
    // relativa a la ubicación del .exe.
    WebRootPath = "wwwroot"
});

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// --- REGISTRO DE SERVICIOS Y CONFIGURACIÓN ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options => 
    options.UseSqlite("Data Source=users.db"));

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddScoped<BiometricoDataService>();
builder.Services.AddScoped<SeatDataService>();
builder.Services.AddScoped<UnificationService>();
builder.Services.AddScoped<ExcelExportService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// --- CONFIGURACIÓN DEL PIPELINE DE HTTP (ORDEN CRÍTICO) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// El orden aquí es MUY importante.
// 1. Usa rutas por defecto (busca index.html).
app.UseDefaultFiles();
// 2. Sirve los archivos encontrados en la carpeta wwwroot.
app.UseStaticFiles();

app.UseCors("AllowAllOrigins");
app.UseAuthorization();

// 3. Mapea los controladores de la API.
app.MapControllers();

// 4. Forzamos el puerto y ejecutamos.
app.Urls.Add("http://localhost:5165");
app.Run();