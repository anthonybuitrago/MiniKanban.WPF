using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Services;

/// <summary>
/// Contrato para validar las reglas de negocio de una tarea antes de persistirla (SRP).
/// </summary>
public interface ITareaValidador
{
    void Validar(Tarea tarea);
}
