using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Repositories;

/// <summary>
/// Abstracción para el acceso a categorías de tareas (DIP).
/// </summary>
public interface ICategoriaRepositorio
{
    Task<List<Categoria>> ObtenerTodasAsync();
    Task AgregarAsync(Categoria categoria);
}
