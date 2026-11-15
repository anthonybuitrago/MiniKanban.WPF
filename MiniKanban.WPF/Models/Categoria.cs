using System.ComponentModel.DataAnnotations;

namespace MiniKanban.WPF.Models;

/// <summary>
/// Categoría a la que puede pertenecer una tarea del tablero Kanban.
/// </summary>
public class Categoria
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(7)]
    public string ColorHex { get; set; } = "#0052CC";

    /// <summary>
    /// Propiedad de navegación para la relación 1:N con tareas.
    /// </summary>
    public ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();

    public override string ToString() => Nombre;
}
