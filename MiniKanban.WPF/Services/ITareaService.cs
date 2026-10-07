using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiniKanban.WPF.Models;

namespace MiniKanban.WPF.Services
{
    public interface ITareaService
    {
        IEnumerable<Tarea> ObtenerTodas();
        void Agregar(Tarea tarea);
        void CambiarEstado(int id, EstadoTarea nuevoEstado);
        void Eliminar(int id);
    }
}