using MiniKanban.WPF.Models;
using MiniKanban.WPF.Services.Interfaces;

namespace MiniKanban.WPF.Services;

/// <summary>
/// Implementación de validaciones de dominio extraídas del ViewModel (Refactoring SRP).
/// </summary>
public class TareaValidador : ITareaValidador
{
    public void Validar(Tarea tarea)
    {
        ArgumentNullException.ThrowIfNull(tarea);

        if (string.IsNullOrWhiteSpace(tarea.Titulo))
            throw new ArgumentException("El título de la tarea es obligatorio.");

        if (tarea.Titulo.Trim().Length < 3)
            throw new ArgumentException("El título debe tener al menos 3 caracteres.");

        if (tarea.Titulo.Length > 120)
            throw new ArgumentException("El título no puede superar los 120 caracteres.");

        if (tarea.Descripcion.Length > 500)
            throw new ArgumentException("La descripción no puede superar los 500 caracteres.");

        if (tarea.CategoriaId <= 0)
            throw new ArgumentException("Debe seleccionar una categoría válida.");

        if (tarea.FechaLimite.Date < DateTime.Today)
            throw new ArgumentException("La fecha límite no puede ser anterior a hoy.");
    }
}
