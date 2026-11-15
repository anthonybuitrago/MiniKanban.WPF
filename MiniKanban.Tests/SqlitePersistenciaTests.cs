using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniKanban.WPF.Data;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.AdoNet;
using MiniKanban.WPF.Repositories.EfCore;

namespace MiniKanban.Tests;

/// <summary>
/// Pruebas de integración con SQLite real aislado por prueba usando EnsureCreated(),
/// cubriendo EF Core (LINQ, AsNoTracking, Change Tracker, DbUpdateException) y ADO.NET.
/// </summary>
public class SqlitePersistenciaTests : IDisposable
{
    private readonly string _rutaDb;
    private readonly string _connectionString;
    private readonly TestDbContextFactory _contextFactory;

    public SqlitePersistenciaTests()
    {
        _rutaDb = Path.Combine(Path.GetTempPath(), $"minikanban_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_rutaDb}";
        _contextFactory = new TestDbContextFactory(_connectionString);

        using var db = _contextFactory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_rutaDb))
        {
            File.Delete(_rutaDb);
        }
    }

    [Fact]
    public async Task EnsureCreated_CreaEsquemaYSiemillaCategoriasIniciales()
    {
        var repoCategorias = new CategoriaRepositorioEfCore(_contextFactory);

        var categorias = await repoCategorias.ObtenerTodasAsync();

        Assert.Equal(4, categorias.Count);
        Assert.Equal(
            new[] { "General", "Personal", "Trabajo", "Universidad" },
            categorias.Select(c => c.Nombre).ToArray());
    }

    [Fact]
    public async Task CategoriaRepositorioEfCore_AgregarAsync_PersisteNuevaCategoria()
    {
        var repoCategorias = new CategoriaRepositorioEfCore(_contextFactory);

        await repoCategorias.AgregarAsync(new Categoria
        {
            Nombre = "Arquitectura",
            ColorHex = "#6554C0"
        });

        var categorias = await repoCategorias.ObtenerTodasAsync();
        Assert.Contains(categorias, c => c.Nombre == "Arquitectura" && c.ColorHex == "#6554C0");
    }

    [Fact]
    public async Task TareaRepositorioEfCore_CrudCompletoYChangeTracker_PersistenEnSqlite()
    {
        var repoTareas = new TareaRepositorioEfCore(_contextFactory);

        var nueva = new Tarea
        {
            Titulo = "Configurar DbContext",
            Descripcion = "Verificar estado Added, Modified y Deleted",
            Prioridad = PrioridadTarea.Alta,
            Estado = EstadoTarea.PorHacer,
            CategoriaId = 1,
            FechaCreacion = DateTime.Now,
            FechaLimite = DateTime.Today.AddDays(2)
        };

        // Added -> INSERT
        await repoTareas.AgregarAsync(nueva);
        Assert.True(nueva.Id > 0);

        // Read + Include (AsNoTracking)
        var leida = await repoTareas.ObtenerPorIdAsync(nueva.Id);
        Assert.NotNull(leida);
        Assert.Equal("Universidad", leida.Categoria?.Nombre);
        Assert.Equal(EstadoTarea.PorHacer, leida.Estado);

        // Modified -> UPDATE
        await repoTareas.CambiarEstadoAsync(nueva.Id, EstadoTarea.EnProgreso);
        var actualizada = await repoTareas.ObtenerPorIdAsync(nueva.Id);
        Assert.Equal(EstadoTarea.EnProgreso, actualizada?.Estado);

        // Deleted -> DELETE
        await repoTareas.EliminarAsync(nueva.Id);
        var eliminada = await repoTareas.ObtenerPorIdAsync(nueva.Id);
        Assert.Null(eliminada);
    }

    [Fact]
    public async Task TareaRepositorioEfCore_AsNoTracking_NoRastreaEntidadesEnDbContext()
    {
        var repoTareas = new TareaRepositorioEfCore(_contextFactory);
        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Consulta sin tracking",
            CategoriaId = 1,
            FechaLimite = DateTime.Today.AddDays(1)
        });

        var resultados = await repoTareas.ObtenerFiltradasAsync();
        resultados[0].Titulo = "Modificado solo en memoria";

        var desdeBd = await repoTareas.ObtenerFiltradasAsync();
        Assert.Equal("Consulta sin tracking", desdeBd[0].Titulo);
    }

    [Fact]
    public async Task TareaRepositorioEfCore_ObtenerFiltradasAsync_AplicaLinqBusquedaPrioridadCategoriaYOrden()
    {
        var repoTareas = new TareaRepositorioEfCore(_contextFactory);

        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Repasar LINQ diferido",
            Descripcion = "Where y OrderBy antes de ToListAsync",
            Prioridad = PrioridadTarea.Alta,
            CategoriaId = 1,
            FechaLimite = DateTime.Today.AddDays(5)
        });
        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Entrega ADO.NET",
            Descripcion = "Usar SqliteDataReader y parámetros",
            Prioridad = PrioridadTarea.Media,
            CategoriaId = 2,
            FechaLimite = DateTime.Today.AddDays(1)
        });
        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Práctica LINQ avanzado",
            Descripcion = "Proyecciones Select",
            Prioridad = PrioridadTarea.Baja,
            CategoriaId = 1,
            FechaLimite = DateTime.Today.AddDays(2)
        });

        // Filtro combinado: texto + categoría 1 + ordenado por fecha límite
        var filtradas = await repoTareas.ObtenerFiltradasAsync(
            textoBusqueda: "LINQ",
            categoriaId: 1,
            ordenarPorFechaLimite: true);

        Assert.Equal(2, filtradas.Count);
        Assert.Equal("Práctica LINQ avanzado", filtradas[0].Titulo);
        Assert.Equal("Repasar LINQ diferido", filtradas[1].Titulo);

        // Filtro por prioridad Alta
        var soloAlta = await repoTareas.ObtenerFiltradasAsync(prioridad: PrioridadTarea.Alta);
        Assert.Single(soloAlta);
        Assert.Equal("Repasar LINQ diferido", soloAlta[0].Titulo);
    }

    [Fact]
    public async Task TareaRepositorioEfCore_ConCategoriaInexistente_LanzaInvalidOperationExceptionPorDbUpdateException()
    {
        var repoTareas = new TareaRepositorioEfCore(_contextFactory);

        var tareaInvalida = new Tarea
        {
            Titulo = "FK inválida",
            CategoriaId = 999,
            FechaLimite = DateTime.Today.AddDays(1)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repoTareas.AgregarAsync(tareaInvalida));
        Assert.IsType<DbUpdateException>(ex.InnerException);
    }

    [Fact]
    public async Task EstadisticasAdoRepositorio_ObtenerResumenYAutoria_ConsultanSqliteConParametrosYDataTable()
    {
        var repoTareas = new TareaRepositorioEfCore(_contextFactory);
        var repoAdo = new EstadisticasAdoRepositorio(_connectionString);
        var hoy = new DateTime(2026, 11, 16);

        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Pendiente vencida y alta",
            Prioridad = PrioridadTarea.Alta,
            Estado = EstadoTarea.PorHacer,
            CategoriaId = 1,
            FechaLimite = hoy.AddDays(-2)
        });
        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "En progreso vigente",
            Prioridad = PrioridadTarea.Media,
            Estado = EstadoTarea.EnProgreso,
            CategoriaId = 2,
            FechaLimite = hoy.AddDays(3)
        });
        await repoTareas.AgregarAsync(new Tarea
        {
            Titulo = "Hecha aunque fecha pasada",
            Prioridad = PrioridadTarea.Alta,
            Estado = EstadoTarea.Hecho,
            CategoriaId = 3,
            FechaLimite = hoy.AddDays(-5)
        });

        // Modo conectado: ExecuteReaderAsync con parámetros
        ResumenKanban resumen = await repoAdo.ObtenerResumenAsync(hoy);
        Assert.Equal(1, resumen.TotalPorHacer);
        Assert.Equal(1, resumen.TotalEnProgreso);
        Assert.Equal(1, resumen.TotalHechas);
        Assert.Equal(1, resumen.TotalPrioridadAlta); // Solo pendientes (excluye Hecho)
        Assert.Equal(1, resumen.TotalVencidas);      // Solo pendientes vencidas (excluye Hecho)

        // Modo desconectado: DataTable sin filtro y con filtro por estado
        DataTable tablaCompleta = await repoAdo.ObtenerTablaAuditoriaAsync();
        Assert.Equal(3, tablaCompleta.Rows.Count);
        Assert.True(tablaCompleta.Columns.Contains("Categoria"));
        Assert.Contains(tablaCompleta.Rows.Cast<DataRow>(), r => (string)r["Categoria"] == "Universidad");

        DataTable tablaHechas = await repoAdo.ObtenerTablaAuditoriaAsync(EstadoTarea.Hecho);
        Assert.Single(tablaHechas.Rows);
        Assert.Equal("Hecha aunque fecha pasada", tablaHechas.Rows[0]["Titulo"]);
        Assert.Equal("Hecho", tablaHechas.Rows[0]["Estado"]);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(string connectionString)
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);
    }
}
