
using Microsoft.EntityFrameworkCore;
using ef_core.Data;
using ef_core.Services;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite("Data Source=users.db"));
builder.Services.AddControllers();
builder.Services.AddScoped<BiometricoDataService>();
builder.Services.AddScoped<SeatDataService>();
builder.Services.AddScoped<UnificationService>();
builder.Services.AddScoped<ExcelExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();


