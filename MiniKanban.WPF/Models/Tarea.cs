using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public class Tarea
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaLimite { get; set; }
        public EstadoTarea Estado { get; set; }
        public PrioridadTarea Prioridad { get; set; }
    }