using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using MiniKanban.WPF.Models;
using MiniKanban.WPF.Repositories;
using MiniKanban.WPF.Services;
using MiniKanban.WPF.ViewModels;
using MiniKanban.WPF.Views;

namespace MiniKanban.Tests;

/// <summary>
/// Pruebas unitarias de MainViewModel ejecutadas 100% en memoria gracias al
/// Principio de Inversión de Dependencias (DIP) y el patrón Repository.
/// </summary>
public class MainViewModelTests
{
    private static (MainViewModel ViewModel, TareaRepositorioMemoria RepoTareas) CrearSut()
    {
        var repoTareas = new TareaRepositorioMemoria();
        var repoCategorias = new CategoriaRepositorioMemoria();
        var repoEstadisticas = new EstadisticasMemoriaRepositorio(repoTareas);
        var validador = new TareaValidador();

        var vm = new MainViewModel(repoTareas, repoCategorias, repoEstadisticas, validador);
        return (vm, repoTareas);
    }

    [Fact]
    public async Task InicializarAsync_CargaCategoriasYResumenEnCero()
    {
        var (vm, _) = CrearSut();

        await vm.InicializarCommand.ExecuteAsync(null);

        Assert.Equal(4, vm.Categorias.Count);
        Assert.Equal(5, vm.CategoriasFiltro.Count); // "Todas las categorías" + 4 categorías
        Assert.NotNull(vm.CategoriaSeleccionada);
        Assert.Equal(0, vm.CategoriaFiltroSeleccionada.Id);
        Assert.Equal(new ResumenKanban(0, 0, 0, 0, 0), vm.Resumen);
        Assert.Equal(string.Empty, vm.MensajeError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]
    public async Task CrearTareaAsync_ConTituloInvalido_MuestraErrorYNoGuarda(string tituloInvalido)
    {
        var (vm, repo) = CrearSut();
        await vm.InicializarCommand.ExecuteAsync(null);

        vm.NuevoTitulo = tituloInvalido;
        await vm.CrearTareaCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(vm.MensajeError));
        Assert.Empty(vm.TareasPorHacer);
        Assert.Empty(await repo.ObtenerFiltradasAsync());
    }

    [Fact]
    public async Task CrearTareaAsync_ConFechaLimitePasada_MuestraErrorYNoGuarda()
    {
        var (vm, repo) = CrearSut();
        await vm.InicializarCommand.ExecuteAsync(null);

        vm.NuevoTitulo = "Tarea vencida";
        vm.NuevaFechaLimite = DateTime.Today.AddDays(-1);

        await vm.CrearTareaCommand.ExecuteAsync(null);

        Assert.Contains("fecha límite", vm.MensajeError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await repo.ObtenerFiltradasAsync());
    }

    [Fact]
    public async Task CrearTareaAsync_ConDatosValidos_AgregaAPorHacerLimpiaFormularioYActualizaResumen()
    {
        var (vm, _) = CrearSut();
        await vm.InicializarCommand.ExecuteAsync(null);

        vm.NuevoTitulo = "Implementar Repository";
        vm.NuevaDescripcion = "Separar acceso a datos en interfaz e implementación EF Core";
        vm.NuevaPrioridad = PrioridadTarea.Alta;
        vm.NuevaFechaLimite = DateTime.Today.AddDays(3);

        await vm.CrearTareaCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.MensajeError);
        Assert.Single(vm.TareasPorHacer);
        Assert.Equal("Implementar Repository", vm.TareasPorHacer[0].Titulo);
        Assert.Equal(string.Empty, vm.NuevoTitulo);
        Assert.Equal(string.Empty, vm.NuevaDescripcion);
        Assert.Equal(PrioridadTarea.Media, vm.NuevaPrioridad);
        Assert.Equal(1, vm.Resumen.TotalPorHacer);
        Assert.Equal(1, vm.Resumen.TotalPrioridadAlta);
    }

    [Fact]
    public async Task AvanzarYRetrocederTareaAsync_MueveCorrectamenteEntreColumnas()
    {
        var (vm, repo) = CrearSut();
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Estudiar LINQ",
            CategoriaId = 1,
            Prioridad = PrioridadTarea.Media,
            Estado = EstadoTarea.PorHacer,
            FechaLimite = DateTime.Today.AddDays(1)
        });
        await vm.InicializarCommand.ExecuteAsync(null);

        // PorHacer -> EnProgreso
        await vm.AvanzarTareaCommand.ExecuteAsync(vm.TareasPorHacer[0]);
        Assert.Empty(vm.TareasPorHacer);
        Assert.Single(vm.TareasEnProgreso);
        Assert.Equal(1, vm.Resumen.TotalEnProgreso);

        // EnProgreso -> Hecho
        await vm.AvanzarTareaCommand.ExecuteAsync(vm.TareasEnProgreso[0]);
        Assert.Empty(vm.TareasEnProgreso);
        Assert.Single(vm.TareasHechas);
        Assert.Equal(1, vm.Resumen.TotalHechas);

        // Avanzar cuando ya está en Hecho no hace cambios
        await vm.AvanzarTareaCommand.ExecuteAsync(vm.TareasHechas[0]);
        Assert.Single(vm.TareasHechas);

        // Hecho -> EnProgreso
        await vm.RetrocederTareaCommand.ExecuteAsync(vm.TareasHechas[0]);
        Assert.Empty(vm.TareasHechas);
        Assert.Single(vm.TareasEnProgreso);

        // EnProgreso -> PorHacer
        await vm.RetrocederTareaCommand.ExecuteAsync(vm.TareasEnProgreso[0]);
        Assert.Single(vm.TareasPorHacer);
        Assert.Empty(vm.TareasEnProgreso);
    }

    [Fact]
    public async Task EliminarTareaAsync_RemueveTareaDelTableroYActualizaResumen()
    {
        var (vm, repo) = CrearSut();
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Tarea temporal",
            CategoriaId = 2,
            Prioridad = PrioridadTarea.Baja,
            Estado = EstadoTarea.PorHacer,
            FechaLimite = DateTime.Today.AddDays(2)
        });
        await vm.InicializarCommand.ExecuteAsync(null);

        await vm.EliminarTareaCommand.ExecuteAsync(vm.TareasPorHacer[0]);

        Assert.Empty(vm.TareasPorHacer);
        Assert.Equal(0, vm.Resumen.TotalPorHacer);
        Assert.Empty(await repo.ObtenerFiltradasAsync());
    }

    [Fact]
    public async Task FiltrosYLimpiarFiltros_FiltranYRestauranTodasLasTareas()
    {
        var (vm, repo) = CrearSut();
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Examen EF Core",
            Descripcion = "Repasar Change Tracker",
            CategoriaId = 1,
            Prioridad = PrioridadTarea.Alta,
            Estado = EstadoTarea.PorHacer,
            FechaLimite = DateTime.Today.AddDays(1)
        });
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Comprar café",
            Descripcion = "Supermercado",
            CategoriaId = 3,
            Prioridad = PrioridadTarea.Baja,
            Estado = EstadoTarea.EnProgreso,
            FechaLimite = DateTime.Today.AddDays(4)
        });
        await vm.InicializarCommand.ExecuteAsync(null);

        // Filtrar por texto + prioridad
        vm.TextoBusqueda = "Change Tracker";
        vm.FiltroPrioridadSeleccionada = vm.FiltrosPrioridad.First(f => f.Valor == PrioridadTarea.Alta);
        await vm.CargarTableroCommand.ExecuteAsync(null);

        Assert.Single(vm.TareasPorHacer);
        Assert.Empty(vm.TareasEnProgreso);
        Assert.Equal("Examen EF Core", vm.TareasPorHacer[0].Titulo);

        // Limpiar filtros debe restaurar ambas tareas
        await vm.LimpiarFiltrosCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.TextoBusqueda);
        Assert.Null(vm.FiltroPrioridadSeleccionada.Valor);
        Assert.Equal(0, vm.CategoriaFiltroSeleccionada.Id);
        Assert.Single(vm.TareasPorHacer);
        Assert.Single(vm.TareasEnProgreso);
    }

    [Fact]
    public async Task AuditoriaAdo_ToggleYEstadoFiltro_GeneraDataTableFiltrada()
    {
        var (vm, repo) = CrearSut();
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Tarea A",
            CategoriaId = 1,
            Estado = EstadoTarea.PorHacer,
            FechaLimite = DateTime.Today.AddDays(1)
        });
        await repo.AgregarAsync(new Tarea
        {
            Titulo = "Tarea B",
            CategoriaId = 2,
            Estado = EstadoTarea.Hecho,
            FechaLimite = DateTime.Today.AddDays(2)
        });
        await vm.InicializarCommand.ExecuteAsync(null);

        Assert.False(vm.MostrarAuditoriaAdo);

        await vm.ToggleAuditoriaAdoCommand.ExecuteAsync(null);

        Assert.True(vm.MostrarAuditoriaAdo);
        Assert.Equal(2, vm.TablaAuditoria.Rows.Count);

        vm.FiltroEstadoAuditoria = vm.FiltrosEstadoAuditoria.First(f => f.Valor == EstadoTarea.Hecho);
        await vm.ActualizarAuditoriaAdoCommand.ExecuteAsync(null);

        Assert.Single(vm.TablaAuditoria.Rows);
        DataRow fila = vm.TablaAuditoria.Rows[0];
        Assert.Equal("Tarea B", fila["Titulo"]);
    }

    [Fact]
    public void MainWindow_InicializaXamlYRecursosSinExcepcion()
    {
        Exception? errorHiloSta = null;

        var hilo = new Thread(() =>
        {
            try
            {
                var (vm, _) = CrearSut();
                var ventana = new MainWindow(vm);
                Assert.Same(vm, ventana.DataContext);
                Assert.NotNull(ventana.FindResource("BoolToVis"));
                ventana.Close();
            }
            catch (Exception ex)
            {
                errorHiloSta = ex;
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();

        if (errorHiloSta is not null)
        {
            ExceptionDispatchInfo.Capture(errorHiloSta).Throw();
        }
    }
}
