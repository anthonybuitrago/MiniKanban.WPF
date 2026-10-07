using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniKanban.WPF.Services;
using System.Collections.ObjectModel;
using MiniKanban.WPF.Models;
using CommunityToolkit.Mvvm.Input;

namespace MiniKanban.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ITareaService _tareaService;

        [ObservableProperty]
        private string _nuevoTitulo = string.Empty;

        [ObservableProperty]
        private string _nuevaDescripcion = string.Empty;

        [ObservableProperty]
        private PrioridadTarea _nuevaPrioridad = PrioridadTarea.Media;

        [ObservableProperty]
        private DateTime _nuevaFechaLimite = DateTime.Today.AddDays(1);

        public MainViewModel(ITareaService tareaService)
        {
            _tareaService = tareaService;
            CargarTareas();
        }
        public ObservableCollection<Tarea> TareasPorHacer { get; } = new();
        public ObservableCollection<Tarea> TareasEnProgreso { get; } = new();
        public ObservableCollection<Tarea> TareasHechas { get; } = new();

        private void CargarTareas()
        {
            TareasPorHacer.Clear();
            TareasEnProgreso.Clear();
            TareasHechas.Clear();

            foreach (var tarea in _tareaService.ObtenerTodas())
            {
                switch (tarea.Estado)
                {
                    case EstadoTarea.PorHacer:
                        TareasPorHacer.Add(tarea);
                        break;

                    case EstadoTarea.EnProgreso:
                        TareasEnProgreso.Add(tarea);
                        break;

                    case EstadoTarea.Hecho:
                        TareasHechas.Add(tarea);
                        break;
                }
            }
        }

        [RelayCommand]
        private void CrearTarea()
        {
            if (string.IsNullOrWhiteSpace(NuevoTitulo))
                return;

            var nuevaTarea = new Tarea
            {
                Titulo = NuevoTitulo,
                Descripcion = NuevaDescripcion,
                Prioridad = NuevaPrioridad,
                FechaLimite = NuevaFechaLimite,
                Estado = EstadoTarea.PorHacer
            };

            _tareaService.Agregar(nuevaTarea);

            NuevoTitulo = string.Empty;
            NuevaDescripcion = string.Empty;

            CargarTareas();
        }

        [RelayCommand]
        private void EliminarTarea(Tarea? tarea)
        {
            if (tarea is null)
                return;

            _tareaService.Eliminar(tarea.Id);
            CargarTareas();
        }

        [RelayCommand]
        private void AvanzarTarea(Tarea? tarea)
        {
            if (tarea is null)
                return;

            switch (tarea.Estado)
            {
                case EstadoTarea.PorHacer:
                    _tareaService.CambiarEstado(tarea.Id, EstadoTarea.EnProgreso);
                    break;

                case EstadoTarea.EnProgreso:
                    _tareaService.CambiarEstado(tarea.Id, EstadoTarea.Hecho);
                    break;
            }

            CargarTareas();
        }

        [RelayCommand]
        private void RetrocederTarea(Tarea? tarea)
        {
            if (tarea is null)
                return;

            switch (tarea.Estado)
            {
                case EstadoTarea.Hecho:
                    _tareaService.CambiarEstado(tarea.Id, EstadoTarea.EnProgreso);
                    break;

                case EstadoTarea.EnProgreso:
                    _tareaService.CambiarEstado(tarea.Id, EstadoTarea.PorHacer);
                    break;
            }

            CargarTareas();
        }
    }
}