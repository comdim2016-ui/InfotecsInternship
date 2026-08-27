using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.Services; // Подключили пространство имен нашего сервиса

var builder = WebApplication.CreateBuilder(args);

// 1. Подключаем поддержку стандартных контроллеров
builder.Services.AddControllers();

// 2. Регистрируем сервис обработки файлов 
builder.Services.AddScoped<FileProcessingService>(); 

// 3. Регистрируем контекст базы данных и связываем его с PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
// 4. Включаем генерацию Swagger 
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