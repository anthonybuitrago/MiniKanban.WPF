using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories;
using MiniKanban.WPF.Services;

namespace MiniKanban.WPF.ViewModels;

public record FiltroPrioridadOpcion(string Etiqueta, PrioridadTarea? Valor);

public record FiltroEstadoOpcion(string Etiqueta, EstadoTarea? Valor);

/// <summary>
/// ViewModel principal que coordina el tablero Kanban de forma asíncrona y desacoplada (DIP + MVVM).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITareaRepositorio _tareaRepositorio;
    private readonly ICategoriaRepositorio _categoriaRepositorio;
    private readonly IEstadisticasRepositorio _estadisticasRepositorio;
    private readonly ITareaValidador _validador;

    [ObservableProperty]
    private string _nuevoTitulo = string.Empty;

    [ObservableProperty]
    private string _nuevaDescripcion = string.Empty;

    [ObservableProperty]
    private PrioridadTarea _nuevaPrioridad = PrioridadTarea.Media;

    [ObservableProperty]
    private DateTime _nuevaFechaLimite = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private Categoria? _categoriaSeleccionada;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private FiltroPrioridadOpcion _filtroPrioridadSeleccionada;

    [ObservableProperty]
    private Categoria _categoriaFiltroSeleccionada;

    [ObservableProperty]
    private bool _ordenarPorFechaLimite = true;

    [ObservableProperty]
    private FiltroEstadoOpcion _filtroEstadoAuditoria;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    private string _mensajeEstado = string.Empty;

    [ObservableProperty]
    private bool _estaCargando;

    [ObservableProperty]
    private ResumenKanban _resumen = new(0, 0, 0, 0, 0);

    [ObservableProperty]
    private DataTable _tablaAuditoria = new();

    [ObservableProperty]
    private bool _mostrarAuditoriaAdo;

    public MainViewModel(
        ITareaRepositorio tareaRepositorio,
        ICategoriaRepositorio categoriaRepositorio,
        IEstadisticasRepositorio estadisticasRepositorio,
        ITareaValidador validador)
    {
        _tareaRepositorio = tareaRepositorio;
        _categoriaRepositorio = categoriaRepositorio;
        _estadisticasRepositorio = estadisticasRepositorio;
        _validador = validador;

        FiltrosPrioridad =
        [
            new FiltroPrioridadOpcion("Todas las prioridades", null),
            new FiltroPrioridadOpcion("Baja", PrioridadTarea.Baja),
            new FiltroPrioridadOpcion("Media", PrioridadTarea.Media),
            new FiltroPrioridadOpcion("Alta", PrioridadTarea.Alta)
        ];
        _filtroPrioridadSeleccionada = FiltrosPrioridad[0];

        FiltrosEstadoAuditoria =
        [
            new FiltroEstadoOpcion("Todos los estados", null),
            new FiltroEstadoOpcion("Por Hacer", EstadoTarea.PorHacer),
            new FiltroEstadoOpcion("En Progreso", EstadoTarea.EnProgreso),
            new FiltroEstadoOpcion("Hecho", EstadoTarea.Hecho)
        ];
        _filtroEstadoAuditoria = FiltrosEstadoAuditoria[0];

        _categoriaFiltroSeleccionada = new Categoria { Id = 0, Nombre = "Todas las categorías", ColorHex = "#5E6C84" };
        CategoriasFiltro.Add(_categoriaFiltroSeleccionada);
    }

    public ObservableCollection<Categoria> Categorias { get; } = new();
    public ObservableCollection<Categoria> CategoriasFiltro { get; } = new();
    public ObservableCollection<Tarea> TareasPorHacer { get; } = new();
    public ObservableCollection<Tarea> TareasEnProgreso { get; } = new();
    public ObservableCollection<Tarea> TareasHechas { get; } = new();

    public IReadOnlyList<PrioridadTarea> PrioridadesDisponibles { get; } = Enum.GetValues<PrioridadTarea>();
    public IReadOnlyList<FiltroPrioridadOpcion> FiltrosPrioridad { get; }
    public IReadOnlyList<FiltroEstadoOpcion> FiltrosEstadoAuditoria { get; }

    [RelayCommand]
    private async Task InicializarAsync()
    {
        await EjecutarConManejoDeErrorAsync(async () =>
        {
            var categorias = await _categoriaRepositorio.ObtenerTodasAsync();

            Categorias.Clear();
            CategoriasFiltro.Clear();

            var opcionTodas = new Categoria { Id = 0, Nombre = "Todas las categorías", ColorHex = "#5E6C84" };
            CategoriasFiltro.Add(opcionTodas);

            foreach (var categoria in categorias)
            {
                Categorias.Add(categoria);
                CategoriasFiltro.Add(categoria);
            }

            CategoriaSeleccionada ??= Categorias.FirstOrDefault();
            CategoriaFiltroSeleccionada = opcionTodas;

            await RecargarDatosInternoAsync();
        });
    }

    [RelayCommand]
    private async Task CargarTableroAsync()
    {
        await EjecutarConManejoDeErrorAsync(RecargarDatosInternoAsync);
    }

    [RelayCommand]
    private async Task LimpiarFiltrosAsync()
    {
        TextoBusqueda = string.Empty;
        FiltroPrioridadSeleccionada = FiltrosPrioridad[0];
        CategoriaFiltroSeleccionada = CategoriasFiltro[0];
        OrdenarPorFechaLimite = true;

        await EjecutarConManejoDeErrorAsync(RecargarDatosInternoAsync);
    }

    [RelayCommand]
    private async Task CrearTareaAsync()
    {
        await EjecutarConManejoDeErrorAsync(async () =>
        {
            var nuevaTarea = new Tarea
            {
                Titulo = NuevoTitulo.Trim(),
                Descripcion = NuevaDescripcion.Trim(),
                Prioridad = NuevaPrioridad,
                FechaLimite = NuevaFechaLimite,
                FechaCreacion = DateTime.Now,
                Estado = EstadoTarea.PorHacer,
                CategoriaId = CategoriaSeleccionada?.Id ?? 0
            };

            _validador.Validar(nuevaTarea);
            await _tareaRepositorio.AgregarAsync(nuevaTarea);

            NuevoTitulo = string.Empty;
            NuevaDescripcion = string.Empty;
            NuevaPrioridad = PrioridadTarea.Media;
            NuevaFechaLimite = DateTime.Today.AddDays(1);

            await RecargarDatosInternoAsync();
        });
    }

    [RelayCommand]
    private Task AvanzarTareaAsync(Tarea? tarea)
        => CambiarEstadoConTransicionAsync(tarea, estado => estado switch
        {
            EstadoTarea.PorHacer => EstadoTarea.EnProgreso,
            EstadoTarea.EnProgreso => EstadoTarea.Hecho,
            _ => null
        });

    [RelayCommand]
    private Task RetrocederTareaAsync(Tarea? tarea)
        => CambiarEstadoConTransicionAsync(tarea, estado => estado switch
        {
            EstadoTarea.Hecho => EstadoTarea.EnProgreso,
            EstadoTarea.EnProgreso => EstadoTarea.PorHacer,
            _ => null
        });

    [RelayCommand]
    private async Task EliminarTareaAsync(Tarea? tarea)
    {
        if (tarea is null)
            return;

        await EjecutarConManejoDeErrorAsync(async () =>
        {
            await _tareaRepositorio.EliminarAsync(tarea.Id);
            await RecargarDatosInternoAsync();
        });
    }

    [RelayCommand]
    private async Task ToggleAuditoriaAdoAsync()
    {
        MostrarAuditoriaAdo = !MostrarAuditoriaAdo;
        if (MostrarAuditoriaAdo)
        {
            await EjecutarConManejoDeErrorAsync(ActualizarAuditoriaInternoAsync);
        }
    }

    [RelayCommand]
    private async Task ActualizarAuditoriaAdoAsync()
    {
        await EjecutarConManejoDeErrorAsync(ActualizarAuditoriaInternoAsync);
    }

    private Task CambiarEstadoConTransicionAsync(Tarea? tarea, Func<EstadoTarea, EstadoTarea?> calcularSiguienteEstado)
    {
        if (tarea is null)
            return Task.CompletedTask;

        EstadoTarea? nuevoEstado = calcularSiguienteEstado(tarea.Estado);
        if (!nuevoEstado.HasValue)
            return Task.CompletedTask;

        return EjecutarConManejoDeErrorAsync(async () =>
        {
            await _tareaRepositorio.CambiarEstadoAsync(tarea.Id, nuevoEstado.Value);
            await RecargarDatosInternoAsync();
        });
    }

    private async Task RecargarDatosInternoAsync()
    {
        int? categoriaIdFiltro = CategoriaFiltroSeleccionada.Id > 0
            ? CategoriaFiltroSeleccionada.Id
            : null;

        var tareas = await _tareaRepositorio.ObtenerFiltradasAsync(
            TextoBusqueda,
            FiltroPrioridadSeleccionada.Valor,
            categoriaIdFiltro,
            OrdenarPorFechaLimite);

        DistribuirEnColumnas(tareas);

        Resumen = await _estadisticasRepositorio.ObtenerResumenAsync(DateTime.Today);
        MensajeEstado = $"Mostrando {tareas.Count} tarea(s)";

        if (MostrarAuditoriaAdo)
        {
            await ActualizarAuditoriaInternoAsync();
        }
    }

    private async Task ActualizarAuditoriaInternoAsync()
    {
        TablaAuditoria = await _estadisticasRepositorio.ObtenerTablaAuditoriaAsync(FiltroEstadoAuditoria.Valor);
    }

    private void DistribuirEnColumnas(IEnumerable<Tarea> tareas)
    {
        TareasPorHacer.Clear();
        TareasEnProgreso.Clear();
        TareasHechas.Clear();

        foreach (var tarea in tareas)
        {
            var columnaDestino = tarea.Estado switch
            {
                EstadoTarea.PorHacer => TareasPorHacer,
                EstadoTarea.EnProgreso => TareasEnProgreso,
                EstadoTarea.Hecho => TareasHechas,
                _ => null
            };

            columnaDestino?.Add(tarea);
        }
    }

    private async Task EjecutarConManejoDeErrorAsync(Func<Task> accion)
    {
        try
        {
            EstaCargando = true;
            MensajeError = string.Empty;
            await accion();
        }
        catch (ArgumentException ex)
        {
            MensajeError = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            MensajeError = $"Error inesperado: {ex.Message}";
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
