using MiniKanban.WPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniKanban.WPF.Services
{
    public class TareaService : ITareaService
    {
        private readonly List<Tarea> _tareas = new();

        private int _siguienteId = 1;

        public void Agregar(Tarea tarea)
        {
            tarea.Id = _siguienteId++;
            _tareas.Add(tarea);
        }

        public void CambiarEstado(int id, EstadoTarea nuevoEstado)
        {
            var tarea = _tareas.FirstOrDefault(t => t.Id == id);
            if (tarea != null)
            {
                tarea.Estado = nuevoEstado;
            }
        }

        public void Eliminar(int id)
        {
            _tareas.RemoveAll(t => t.Id == id);
        }

        public IEnumerable<Tarea> ObtenerTodas()
        {
            return _tareas;
        }
    }
}