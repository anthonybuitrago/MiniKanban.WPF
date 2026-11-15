using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Repositories;

/// <summary>
/// Implementación en memoria de ITareaRepositorio (doble de prueba para demostrar DIP sin BD real).
/// </summary>
public class TareaRepositorioMemoria : ITareaRepositorio
{
    private readonly List<Tarea> _tareas = new();
    private readonly Dictionary<int, Categoria> _categorias;
    private int _siguienteId = 1;

    public TareaRepositorioMemoria(IEnumerable<Categoria>? categoriasIniciales = null)
    {
        var lista = categoriasIniciales ??
        [
            new Categoria { Id = 1, Nombre = "Universidad", ColorHex = "#0052CC" },
            new Categoria { Id = 2, Nombre = "Trabajo", ColorHex = "#00875A" },
            new Categoria { Id = 3, Nombre = "Personal", ColorHex = "#FF8B00" },
            new Categoria { Id = 4, Nombre = "General", ColorHex = "#5E6C84" }
        ];

        _categorias = lista.ToDictionary(c => c.Id);
    }

    public Task<List<Tarea>> ObtenerFiltradasAsync(
        string? textoBusqueda = null,
        PrioridadTarea? prioridad = null,
        int? categoriaId = null,
        bool ordenarPorFechaLimite = true)
    {
        IEnumerable<Tarea> consulta = _tareas;

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            string termino = textoBusqueda.Trim();
            consulta = consulta.Where(t =>
                t.Titulo.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                t.Descripcion.Contains(termino, StringComparison.OrdinalIgnoreCase));
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

        // Retornamos copias desacopladas simulando AsNoTracking
        var resultado = consulta.Select(ClonarConCategoria).ToList();
        return Task.FromResult(resultado);
    }

    public Task<Tarea?> ObtenerPorIdAsync(int id)
    {
        var tarea = _tareas.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(tarea is null ? null : ClonarConCategoria(tarea));
    }

    public Task AgregarAsync(Tarea tarea)
    {
        ArgumentNullException.ThrowIfNull(tarea);

        if (!_categorias.ContainsKey(tarea.CategoriaId))
            throw new InvalidOperationException($"La categoría {tarea.CategoriaId} no existe.");

        tarea.Id = _siguienteId++;
        _tareas.Add(ClonarConCategoria(tarea));
        return Task.CompletedTask;
    }

    public Task CambiarEstadoAsync(int id, EstadoTarea nuevoEstado)
    {
        var tarea = _tareas.FirstOrDefault(t => t.Id == id);
        if (tarea is not null)
        {
            tarea.Estado = nuevoEstado;
        }
        return Task.CompletedTask;
    }

    public Task EliminarAsync(int id)
    {
        _tareas.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }

    private Tarea ClonarConCategoria(Tarea origen)
    {
        _categorias.TryGetValue(origen.CategoriaId, out var cat);
        return new Tarea
        {
            Id = origen.Id,
            Titulo = origen.Titulo,
            Descripcion = origen.Descripcion,
            FechaCreacion = origen.FechaCreacion,
            FechaLimite = origen.FechaLimite,
            Estado = origen.Estado,
            Prioridad = origen.Prioridad,
            CategoriaId = origen.CategoriaId,
            Categoria = cat
        };
    }
}
