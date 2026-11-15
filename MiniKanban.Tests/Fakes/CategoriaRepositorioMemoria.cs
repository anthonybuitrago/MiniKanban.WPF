using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.Interfaces;

namespace MiniKanban.Tests.Fakes;

/// <summary>
/// Implementación en memoria de ICategoriaRepositorio para pruebas unitarias sin BD.
/// </summary>
public class CategoriaRepositorioMemoria : ICategoriaRepositorio
{
    private readonly List<Categoria> _categorias;
    private int _siguienteId;

    public CategoriaRepositorioMemoria(IEnumerable<Categoria>? categoriasIniciales = null)
    {
        _categorias = (categoriasIniciales ??
        [
            new Categoria { Id = 1, Nombre = "Universidad", ColorHex = "#0052CC" },
            new Categoria { Id = 2, Nombre = "Trabajo", ColorHex = "#00875A" },
            new Categoria { Id = 3, Nombre = "Personal", ColorHex = "#FF8B00" },
            new Categoria { Id = 4, Nombre = "General", ColorHex = "#5E6C84" }
        ]).ToList();

        _siguienteId = _categorias.Count == 0 ? 1 : _categorias.Max(c => c.Id) + 1;
    }

    public Task<List<Categoria>> ObtenerTodasAsync()
        => Task.FromResult(_categorias.OrderBy(c => c.Nombre).ToList());

    public Task AgregarAsync(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        categoria.Id = _siguienteId++;
        _categorias.Add(categoria);
        return Task.CompletedTask;
    }
}
