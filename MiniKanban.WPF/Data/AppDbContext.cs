using Microsoft.EntityFrameworkCore;
using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Data;

/// <summary>
/// Contexto de Entity Framework Core que representa la sesión con la base de datos SQLite.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<Categoria> Categorias => Set<Categoria>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=kanban.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tarea>()
            .HasOne(t => t.Categoria)
            .WithMany(c => c.Tareas)
            .HasForeignKey(t => t.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Datos semilla iniciales para categorías
        modelBuilder.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Universidad", ColorHex = "#0052CC" },
            new Categoria { Id = 2, Nombre = "Trabajo", ColorHex = "#00875A" },
            new Categoria { Id = 3, Nombre = "Personal", ColorHex = "#FF8B00" },
            new Categoria { Id = 4, Nombre = "General", ColorHex = "#5E6C84" }
        );
    }
}
