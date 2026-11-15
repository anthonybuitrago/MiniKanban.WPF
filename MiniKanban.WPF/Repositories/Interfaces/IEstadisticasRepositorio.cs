using System.Data;
using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Repositories.Interfaces;

/// <summary>
/// Abstracción para consultas de reporte/auditoría implementada con ADO.NET puro.
/// </summary>
public interface IEstadisticasRepositorio
{
    Task<ResumenKanban> ObtenerResumenAsync(DateTime fechaReferencia);
    Task<DataTable> ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null);
}
