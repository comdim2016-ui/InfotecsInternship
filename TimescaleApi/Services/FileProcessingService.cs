using System.Globalization;
using TimescaleApi.Models;

namespace TimescaleApi.Services;

public class FileProcessingService
{
    public (Result Result, List<ValueRecord> Values) ParseAndValidateCsv(Stream csvStream, string fileName)
    {
        var values = new List<ValueRecord>();
        using var reader = new StreamReader(csvStream);
        
        // Читаем заголовок (первая строка: Date;ExecutionTime;Value)
        string? header = reader.ReadLine();
        if (header == null)
            throw new ArgumentException("Файл пуст.");

        // Проверяем формат заголовка
        var headerParts = header.Split(';');
        if (headerParts.Length < 3 || headerParts[0].Trim() != "Date" || headerParts[1].Trim() != "ExecutionTime" || headerParts[2].Trim() != "Value")
        {
            throw new ArgumentException("Неверный формат заголовка CSV. Ожидается: Date;ExecutionTime;Value");
        }

        int lineCount = 0;
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            lineCount++;
            if (lineCount > 10000)
            {
                throw new ArgumentException("Количество строк в файле не может быть больше 10 000.");
            }

            var parts = line.Split(';');
            if (parts.Length < 3)
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Отсутствуют обязательные значения.");
            }

            // 1. Парсим и валидируем дату
            if (!DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out DateTime date))
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Некорректный формат даты.");
            }
            
            // Приводим к UTC, так как Postgres хранит timestamp с временной зоной
            date = date.ToUniversalTime(); 

            if (date > DateTime.UtcNow)
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Дата не может быть позже текущей.");
            }
            if (date < new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Дата не может быть раньше 01.01.2000.");
            }

            // 2. Парсим и валидируем ExecutionTime
            if (!double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double executionTime))
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Некорректный формат времени выполнения.");
            }
            if (executionTime < 0)
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Время выполнения не может быть меньше 0.");
            }

            // 3. Парсим и валидируем Value 
            if (!double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double valueAmount))
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Некорректный формат значения показателя.");
            }
            if (valueAmount < 0)
            {
                throw new ArgumentException($"Строка {lineCount + 1}: Значение показателя не может быть меньше 0.");
            }

            // Добавляем проверенную строчку в список
            values.Add(new ValueRecord
            {
                Date = date,
                ExecutionTime = executionTime,
                Value = valueAmount
            });
        }

        if (lineCount < 1)
        {
            throw new ArgumentException("Файл не содержит записей.");
        }

        // Расчет статистики 
        var minDate = values.Min(x => x.Date);
        var maxDate = values.Max(x => x.Date);
        var timeDelta = (maxDate - minDate).TotalSeconds;

        var averageExecutionTime = values.Average(x => x.ExecutionTime);
        var averageValue = values.Average(x => x.Value);

        // Расчет медианы 
        var sortedValues = values.Select(x => x.Value).OrderBy(v => v).ToList();
        double medianValue;
        int count = sortedValues.Count;
        if (count % 2 == 0)
            medianValue = (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2.0;
        else
            medianValue = sortedValues[count / 2];

        var maxValue = values.Max(x => x.Value);
        var minValue = values.Min(x => x.Value);

        // Собираем объект Result, который запишется в таблицу Results
        var result = new Result
        {
            FileName = fileName,
            TimeDelta = timeDelta,
            MinDate = minDate,
            AverageExecutionTime = averageExecutionTime,
            AverageValue = averageValue,
            MedianValue = medianValue,
            MaxValue = maxValue,
            MinValue = minValue,
            Values = values // Автоматически связываем записи
        };

        return (result, values);
    }
}