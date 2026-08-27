namespace TimescaleApi.Models;

public class ValueRecord
{
    public int Id { get; set; } // Первичный ключ
    public int ResultId { get; set; } // Внешний ключ (связь с таблицей Results)
    public DateTime Date { get; set; } 
    public double ExecutionTime { get; set; } 
    public double Value { get; set; } 

    // Навигационное свойство к родителю
    public Result Result { get; set; } = null!;
}