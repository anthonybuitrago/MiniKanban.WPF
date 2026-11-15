using System.Data;
using Microsoft.Data.Sqlite;
using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Repositories;

/// <summary>
/// Acceso a datos de bajo nivel usando ADO.NET puro (Connection, Command parametrizado, DataReader y DataTable)
/// para métricas agregadas y reportes de auditoría sobre la base de datos SQLite.
/// </summary>
public class EstadisticasAdoRepositorio : IEstadisticasRepositorio
{
    private readonly string _connectionString;

    public EstadisticasAdoRepositorio(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Modo conectado: SqliteConnection + SqliteCommand parametrizado + SqliteDataReader.
    /// </summary>
    public async Task<ResumenKanban> ObtenerResumenAsync(DateTime fechaReferencia)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN Estado = @porHacer THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Estado = @enProgreso THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Estado = @hecho THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Prioridad = @alta AND Estado <> @hecho THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN date(FechaLimite) < date(@fechaRef) AND Estado <> @hecho THEN 1 ELSE 0 END), 0)
            FROM Tareas;
            """;

        cmd.Parameters.AddWithValue("@porHacer", (int)EstadoTarea.PorHacer);
        cmd.Parameters.AddWithValue("@enProgreso", (int)EstadoTarea.EnProgreso);
        cmd.Parameters.AddWithValue("@hecho", (int)EstadoTarea.Hecho);
        cmd.Parameters.AddWithValue("@alta", (int)PrioridadTarea.Alta);
        cmd.Parameters.AddWithValue("@fechaRef", fechaReferencia.ToString("yyyy-MM-dd"));

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ResumenKanban(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4));
        }

        return new ResumenKanban(0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Modo desconectado: carga los resultados en un DataTable en memoria para enlazarlo con DataGrid.
    /// </summary>
    public async Task<DataTable> ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null)
    {
        var tabla = new DataTable("AuditoriaTareas");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        var sql = """
            SELECT
                t.Id AS ID,
                t.Titulo AS Titulo,
                c.Nombre AS Categoria,
                CASE t.Estado
                    WHEN 0 THEN 'Por Hacer'
                    WHEN 1 THEN 'En Progreso'
                    ELSE 'Hecho'
                END AS Estado,
                CASE t.Prioridad
                    WHEN 0 THEN 'Baja'
                    WHEN 1 THEN 'Media'
                    ELSE 'Alta'
                END AS Prioridad,
                strftime('%Y-%m-%d', t.FechaLimite) AS FechaLimite
            FROM Tareas t
            INNER JOIN Categorias c ON t.CategoriaId = c.Id
            """;

        if (estadoFiltro.HasValue)
        {
            sql += " WHERE t.Estado = @estado";
            cmd.Parameters.AddWithValue("@estado", (int)estadoFiltro.Value);
        }

        sql += " ORDER BY t.Id DESC;";
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync();
        tabla.Load(reader);
        return tabla;
    }
}
