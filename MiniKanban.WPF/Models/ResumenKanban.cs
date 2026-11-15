namespace MiniKanban.WPF.Models;

/// <summary>
/// Proyección inmutable con las métricas agregadas del tablero Kanban.
/// </summary>
public record ResumenKanban(
    int TotalPorHacer,
    int TotalEnProgreso,
    int TotalHechas,
    int TotalPrioridadAlta,
    int TotalVencidas);
