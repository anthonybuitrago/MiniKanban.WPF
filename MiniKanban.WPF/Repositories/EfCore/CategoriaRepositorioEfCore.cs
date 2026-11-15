using Microsoft.EntityFrameworkCore;
using MiniKanban.WPF.Data;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.Interfaces;

namespace MiniKanban.WPF.Repositories.EfCore;

/// <summary>
/// Repositorio de categorías respaldado por Entity Framework Core.
/// </summary>
public class CategoriaRepositorioEfCore : ICategoriaRepositorio
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CategoriaRepositorioEfCore(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Categoria>> ObtenerTodasAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task AgregarAsync(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Categorias.Add(categoria);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("No se pudo guardar la categoría en la base de datos.", ex);
        }
    }
}
