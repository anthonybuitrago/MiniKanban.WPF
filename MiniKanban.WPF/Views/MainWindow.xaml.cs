using System.Windows;
using MiniKanban.WPF.ViewModels;

namespace MiniKanban.WPF.Views;

/// <summary>
/// Ventana principal del tablero Kanban. Recibe su ViewModel por Inyección de Dependencias.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await _viewModel.InicializarCommand.ExecuteAsync(null);
    }
}
