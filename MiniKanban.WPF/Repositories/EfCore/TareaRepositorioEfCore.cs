using Microsoft.EntityFrameworkCore;
using MiniKanban.WPF.Data;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.Interfaces;

namespace MiniKanban.WPF.Repositories.EfCore;

/// <summary>
/// Implementación del patrón Repository respaldada por Entity Framework Core y SQLite.
/// </summary>
public class TareaRepositorioEfCore : ITareaRepositorio
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TareaRepositorioEfCore(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Tarea>> ObtenerFiltradasAsync(
        string? textoBusqueda = null,
        PrioridadTarea? prioridad = null,
        int? categoriaId = null,
        bool ordenarPorFechaLimite = true)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Lectura de solo consulta: AsNoTracking evita sobrecarga del Change Tracker
        IQueryable<Tarea> consulta = db.Tareas
            .AsNoTracking()
            .Include(t => t.Categoria);

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            string patron = $"%{textoBusqueda.Trim()}%";
            consulta = consulta.Where(t =>
                EF.Functions.Like(t.Titulo, patron) ||
                EF.Functions.Like(t.Descripcion, patron));
        }

        if (prioridad.HasValue)
        {
            consulta = consulta.Where(t => t.Prioridad == prioridad.Value);
        }

        if (categoriaId.HasValue && categoriaId.Value > 0)
        {
            consulta = consulta.Where(t => t.CategoriaId == categoriaId.Value);
        }

        consulta = ordenarPorFechaLimite
            ? consulta.OrderBy(t => t.FechaLimite).ThenByDescending(t => t.Prioridad)
            : consulta.OrderByDescending(t => t.Prioridad).ThenByDescending(t => t.Id);

        // Ejecución diferida: recién aquí EF traduce la consulta LINQ a SQL
        return await consulta.ToListAsync();
    }

    public async Task<Tarea?> ObtenerPorIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Tareas
            .AsNoTracking()
            .Include(t => t.Categoria)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AgregarAsync(Tarea tarea)
    {
        ArgumentNullException.ThrowIfNull(tarea);

        await using var db = await _contextFactory.CreateDbContextAsync();

        // Desacoplar la propiedad de navegación para evitar que EF intente reinsertar la categoría
        tarea.Categoria = null;

        // Estado Added en el Change Tracker -> generará INSERT al llamar SaveChangesAsync
        db.Tareas.Add(tarea);
        await GuardarCambiosAsync(db);
    }

    public async Task CambiarEstadoAsync(int id, EstadoTarea nuevoEstado)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Aquí SÍ rastreamos la entidad para que el Change Tracker detecte la modificación
        var tarea = await db.Tareas.FindAsync(id);
        if (tarea is null || tarea.Estado == nuevoEstado)
            return;

        // El Change Tracker marca la entidad como Modified -> generará UPDATE
        tarea.Estado = nuevoEstado;
        await GuardarCambiosAsync(db);
    }

    public async Task EliminarAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var tarea = await db.Tareas.FindAsync(id);
        if (tarea is null)
            return;

        // El Change Tracker marca la entidad como Deleted -> generará DELETE
        db.Tareas.Remove(tarea);
        await GuardarCambiosAsync(db);
    }

    private static async Task GuardarCambiosAsync(AppDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Error al guardar los cambios en SQLite. Verifique las restricciones de datos.",
                ex);
        }
    }
}
