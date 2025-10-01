using Microsoft.EntityFrameworkCore;
using ef_core.Data;
using ef_core.Services;
using OfficeOpenXml;
using System.Text.Json.Serialization;
using utils;

var builder = WebApplication.CreateBuilder(args);

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CONFIGURACIÓN DE BASE DE DATOS ---
builder.Services.AddDbContext<ApplicationDbContext>(options => 
    options.UseSqlite("Data Source=users.db")); // Tu configuración de SQLite está correcta.

// --- CONFIGURACIÓN DE CONTROLADORES Y JSON (AQUÍ ESTÁ LA CORRECCIÓN) ---
builder.Services.AddControllers().AddJsonOptions(options =>
    {
        // Se añade el convertidor que fuerza a las fechas a ser tratadas como UTC.
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()); 
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- REGISTRO DE SERVICIOS ---
builder.Services.AddScoped<BiometricoDataService>();
builder.Services.AddScoped<SeatDataService>();
builder.Services.AddScoped<UnificationService>();
builder.Services.AddScoped<ExcelExportService>();

// --- CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 1. Habilita el uso de archivos por defecto, como index.html
app.UseDefaultFiles();

// 2. Habilita la capacidad de servir archivos estáticos desde la carpeta wwwroot
app.UseStaticFiles();

app.UseCors("AllowAllOrigins");
app.UseAuthorization();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
