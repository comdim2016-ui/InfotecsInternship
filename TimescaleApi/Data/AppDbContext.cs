using Microsoft.EntityFrameworkCore;
using TimescaleApi.Models;

namespace TimescaleApi.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {  
    }

    // Две таблицы в базе данных
    public DbSet<Result> Results { get; set; } = null!;
    public DbSet<ValueRecord> Values { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настраиваем связь "один ко многим"
        modelBuilder.Entity<Result>()
            .HasMany(r => r.Values)
            .WithOne(v => v.Result)
            .HasForeignKey(v => v.ResultId)
            .OnDelete(DeleteBehavior.Cascade); 

        // Сделаем имя файла уникальным индексом для быстрого поиска
        modelBuilder.Entity<Result>()
            .HasIndex(r => r.FileName)
            .IsUnique();
    }
}