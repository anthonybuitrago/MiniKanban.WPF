using System.Data;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories.Interfaces;

namespace MiniKanban.Tests.Fakes;

/// <summary>
/// Implementación en memoria de IEstadisticasRepositorio usando LINQ para pruebas sin SQLite.
/// </summary>
public class EstadisticasMemoriaRepositorio : IEstadisticasRepositorio
{
    private readonly ITareaRepositorio _tareaRepositorio;

    public EstadisticasMemoriaRepositorio(ITareaRepositorio tareaRepositorio)
    {
        _tareaRepositorio = tareaRepositorio;
    }

    public async Task<ResumenKanban> ObtenerResumenAsync(DateTime fechaReferencia)
    {
        var tareas = await _tareaRepositorio.ObtenerFiltradasAsync();

        return new ResumenKanban(
            TotalPorHacer: tareas.Count(t => t.Estado == EstadoTarea.PorHacer),
            TotalEnProgreso: tareas.Count(t => t.Estado == EstadoTarea.EnProgreso),
            TotalHechas: tareas.Count(t => t.Estado == EstadoTarea.Hecho),
            TotalPrioridadAlta: tareas.Count(t => t.Prioridad == PrioridadTarea.Alta && t.Estado != EstadoTarea.Hecho),
            TotalVencidas: tareas.Count(t => t.FechaLimite.Date < fechaReferencia.Date && t.Estado != EstadoTarea.Hecho));
    }

    public async Task<DataTable> ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null)
    {
        var tareas = await _tareaRepositorio.ObtenerFiltradasAsync();
        IEnumerable<Tarea> filtradas = estadoFiltro.HasValue
            ? tareas.Where(t => t.Estado == estadoFiltro.Value)
            : tareas;

        var tabla = new DataTable("AuditoriaTareas");
        tabla.Columns.Add("ID", typeof(int));
        tabla.Columns.Add("Titulo", typeof(string));
        tabla.Columns.Add("Categoria", typeof(string));
        tabla.Columns.Add("Estado", typeof(string));
        tabla.Columns.Add("Prioridad", typeof(string));
        tabla.Columns.Add("FechaLimite", typeof(string));

        foreach (var t in filtradas.OrderByDescending(x => x.Id))
        {
            tabla.Rows.Add(
                t.Id,
                t.Titulo,
                t.Categoria?.Nombre ?? "General",
                t.Estado.ToString(),
                t.Prioridad.ToString(),
                t.FechaLimite.ToString("yyyy-MM-dd"));
        }

        return tabla;
    }
}
