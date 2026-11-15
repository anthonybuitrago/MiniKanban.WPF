using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniKanban.WPF.Data;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.AdoNet;
using MiniKanban.WPF.Repositories.EfCore;
using MiniKanban.WPF.Repositories.Interfaces;
using MiniKanban.WPF.Services;
using MiniKanban.WPF.Services.Interfaces;
using MiniKanban.WPF.ViewModels;
using MiniKanban.WPF.Views;

namespace MiniKanban.WPF;

/// <summary>
/// Punto de entrada de la aplicación. Configura el contenedor de Inyección de Dependencias (DIP)
/// e inicializa la base de datos SQLite mediante EnsureCreated().
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigurarServicios(services);

        _serviceProvider = services.BuildServiceProvider();

        InicializarBaseDeDatos(_serviceProvider);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigurarServicios(IServiceCollection services)
    {
        string rutaDb = Path.Combine(AppContext.BaseDirectory, "kanban.db");
        string connectionString = $"Data Source={rutaDb}";

        // EF Core + SQLite
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

        // Reglas de negocio (SRP)
        services.AddSingleton<ITareaValidador, TareaValidador>();

        // Patrón Repository (DIP: cambiar EF Core por Memoria no afecta al ViewModel)
        services.AddSingleton<ITareaRepositorio, TareaRepositorioEfCore>();
        services.AddSingleton<ICategoriaRepositorio, CategoriaRepositorioEfCore>();

        // ADO.NET puro para métricas rápidas y tabla de auditoría
        services.AddSingleton<IEstadisticasRepositorio>(_ => new EstadisticasAdoRepositorio(connectionString));

        // ViewModels y Vistas
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static void InicializarBaseDeDatos(IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();

        // EnsureCreated() crea la BD y las categorías semilla (HasData) si no existe
        if (db.Database.EnsureCreated())
        {
            db.Tareas.AddRange(
                new Tarea
                {
                    Titulo = "Preparar presentación INF033",
                    Descripcion = "Explicar EF Core, Change Tracker, LINQ y ADO.NET.",
                    Prioridad = PrioridadTarea.Alta,
                    Estado = EstadoTarea.EnProgreso,
                    CategoriaId = 1,
                    FechaCreacion = DateTime.Now,
                    FechaLimite = DateTime.Today.AddDays(1)
                },
                new Tarea
                {
                    Titulo = "Revisar pruebas unitarias (DIP)",
                    Descripcion = "Verificar ejecución de tests con TareaRepositorioMemoria sin BD.",
                    Prioridad = PrioridadTarea.Media,
                    Estado = EstadoTarea.PorHacer,
                    CategoriaId = 1,
                    FechaCreacion = DateTime.Now,
                    FechaLimite = DateTime.Today.AddDays(2)
                },
                new Tarea
                {
                    Titulo = "Configurar SQLite con EnsureCreated",
                    Descripcion = "Persistir tareas y categorías localmente en kanban.db.",
                    Prioridad = PrioridadTarea.Baja,
                    Estado = EstadoTarea.Hecho,
                    CategoriaId = 2,
                    FechaCreacion = DateTime.Now.AddDays(-1),
                    FechaLimite = DateTime.Today
                });

            db.SaveChanges();
        }
    }
}
