using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.Services; // Подключили пространство имен нашего сервиса

var builder = WebApplication.CreateBuilder(args);

// Подключаем поддержку стандартных контроллеров
builder.Services.AddControllers();

// Регистрируем сервис обработки файлов 
builder.Services.AddScoped<FileProcessingService>(); 

// Регистрируем контекст базы данных и связываем его с PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
// Включаем генерацию Swagger 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Настраиваем конвейер обработки HTTP-запросов 
if (app.Environment.IsDevelopment())
{
    // Включаем Swagger 
    app.UseSwagger();
    app.UseSwaggerUI(); 
}

app.UseHttpsRedirection();

// Направляем запросы на наши контроллеры
app.MapControllers();

app.Run();