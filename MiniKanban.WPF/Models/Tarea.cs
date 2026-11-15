using System.ComponentModel.DataAnnotations;

namespace MiniKanban.WPF.Models;

public enum EstadoTarea
{
    PorHacer,
    EnProgreso,
    Hecho
}

public enum PrioridadTarea
{
    Baja,
    Media,
    Alta
}

/// <summary>
/// Entidad principal del dominio persistida mediante EF Core.
/// </summary>
public class Tarea
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Titulo { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime FechaLimite { get; set; } = DateTime.Today.AddDays(1);

    public EstadoTarea Estado { get; set; } = EstadoTarea.PorHacer;

    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Media;

    /// <summary>
    /// Clave foránea hacia la categoría asignada.
    /// </summary>
    public int CategoriaId { get; set; }

    /// <summary>
    /// Propiedad de navegación hacia la categoría.
    /// </summary>
    public Categoria? Categoria { get; set; }
}
