using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Repositories;

/// <summary>
/// Abstracción del patrón Repository para el acceso a tareas (DIP).
/// Permite intercambiar EF Core por un repositorio en memoria sin modificar el ViewModel.
/// </summary>
public interface ITareaRepositorio
{
    Task<List<Tarea>> ObtenerFiltradasAsync(
        string? textoBusqueda = null,
        PrioridadTarea? prioridad = null,
        int? categoriaId = null,
        bool ordenarPorFechaLimite = true);

    Task<Tarea?> ObtenerPorIdAsync(int id);
    Task AgregarAsync(Tarea tarea);
    Task CambiarEstadoAsync(int id, EstadoTarea nuevoEstado);
    Task EliminarAsync(int id);
}
