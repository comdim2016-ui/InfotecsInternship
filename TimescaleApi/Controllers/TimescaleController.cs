using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.Models;
using TimescaleApi.Services;

namespace TimescaleApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimescaleController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileProcessingService _fileService;

    public TimescaleController(AppDbContext context, FileProcessingService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    // 1. Метод загрузки, валидации и сохранения CSV
    [HttpPost("upload")]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не выбран или пуст.");
        }

        if (Path.GetExtension(file.FileName).ToLower() != ".csv")
        {
            return BadRequest("Неверный формат файла. Разрешены только .csv файлы.");
        }

        try
        {
            using var stream = file.OpenReadStream();
            
            // Парсим и валидируем файл 
            var (result, values) = _fileService.ParseAndValidateCsv(stream, file.FileName);

            // Начинаем транзакцию в базе данных
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Проверяем, существует ли файл с таким именем в базе. 
                // Благодаря каскадному удалению, удалив Result, мы автоматически сотрем все старые Values
                var existingResult = await _context.Results.FirstOrDefaultAsync(r => r.FileName == file.FileName);
                if (existingResult != null)
                {
                    _context.Results.Remove(existingResult);
                    await _context.SaveChangesAsync();
                }

                // Сохраняем новые данные
                _context.Results.Add(result);
                await _context.SaveChangesAsync();

                // Подтверждаем транзакцию
                await transaction.CommitAsync();
            }
            catch (Exception dbEx)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Ошибка при сохранении в базу данных: {dbEx.Message}");
            }

            return Ok(new { Message = "Файл успешно обработан и сохранен в базе данных.", FileId = result.Id });
        }
        catch (ArgumentException valEx)
        {
            return BadRequest(valEx.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
        }
    }

    // 2. Метод получения списка результатов с фильтрами
    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        [FromQuery] string? fileName,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] double? minAvgValue,
        [FromQuery] double? maxAvgValue,
        [FromQuery] double? minAvgExecutionTime,
        [FromQuery] double? maxAvgExecutionTime)
    {
        IQueryable<Result> query = _context.Results;

        // Применяем фильтр по имени файла 
        if (!string.IsNullOrEmpty(fileName))
        {
            query = query.Where(r => r.FileName.Contains(fileName));
        }

        // Фильтр по времени запуска первой операции 
        if (dateFrom.HasValue)
        {
            query = query.Where(r => r.MinDate >= dateFrom.Value.ToUniversalTime());
        }
        if (dateTo.HasValue)
        {
            query = query.Where(r => r.MinDate <= dateTo.Value.ToUniversalTime());
        }

        // Фильтр по среднему показателю
        if (minAvgValue.HasValue)
        {
            query = query.Where(r => r.AverageValue >= minAvgValue.Value);
        }
        if (maxAvgValue.HasValue)
        {
            query = query.Where(r => r.AverageValue <= maxAvgValue.Value);
        }

        // Фильтр по среднему времени выполнения 
        if (minAvgExecutionTime.HasValue)
        {
            query = query.Where(r => r.AverageExecutionTime >= minAvgExecutionTime.Value);
        }
        if (maxAvgExecutionTime.HasValue)
        {
            query = query.Where(r => r.AverageExecutionTime <= maxAvgExecutionTime.Value);
        }

        var filteredResults = await query.ToListAsync();
        return Ok(filteredResults);
    }

    // 3. Метод получения последних 10 значений для файла
    [HttpGet("values/{fileName}")]
    public async Task<IActionResult> GetLastTenValues(string fileName)
    {
        // Проверяем, существует ли такой файл в базе
        var fileExists = await _context.Results.AnyAsync(r => r.FileName == fileName);
        if (!fileExists)
        {
            return NotFound($"Файл с именем '{fileName}' не найден в базе данных.");
        }

        // Берем последние 10 записей, отсортированных по времени запуска 
        var lastValues = await _context.Values
            .Where(v => v.Result.FileName == fileName)
            .OrderByDescending(v => v.Date) 
            .Take(10)                       
            .OrderBy(v => v.Date)           
            .Select(v => new
            {
                v.Id,
                v.Date,
                v.ExecutionTime,
                v.Value
            })
            .ToListAsync();

        return Ok(lastValues);
    }
}