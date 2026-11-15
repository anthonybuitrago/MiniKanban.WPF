# MiniKanban.WPF

Aplicación de escritorio estilo **Tablero Kanban** desarrollada con **WPF (.NET 8)**, **MVVM** (`CommunityToolkit.Mvvm`), **Entity Framework Core + SQLite**, **LINQ**, **ADO.NET**, **Inyección de Dependencias (DIP)** y **pruebas automatizadas con xUnit**.

---

## Características implementadas

- **Tablero Kanban en 3 columnas:**
  - **Por Hacer** (`EstadoTarea.PorHacer`)
  - **En Progreso** (`EstadoTarea.EnProgreso`)
  - **Hecho** (`EstadoTarea.Hecho`, con tachado visual automático mediante `DataTrigger`)
- **Persistencia local con Entity Framework Core + SQLite (`EnsureCreated`):**
  - Entidades `Tarea` y `Categoria` con Data Annotations y relación 1:N (`DeleteBehavior.Restrict`).
  - Inicialización automática de `kanban.db` mediante `Database.EnsureCreated()` con categorías semilla (`HasData`) y tareas iniciales de ejemplo.
  - Consultas de lectura optimizadas con `AsNoTracking()`, carga de relación con `Include()` y gestión explícita del **Change Tracker** (`Added`, `Modified`, `Deleted`) capturando `DbUpdateException`.
- **Consultas dinámicas con LINQ:**
  - Filtro por texto (`EF.Functions.Like` / búsqueda en título y descripción), prioridad, categoría y ordenación dinámica antes de materializar con `ToListAsync()`.
- **Reportes y Auditoría con ADO.NET puro:**
  - **Modo conectado:** `SqliteConnection`, `SqliteCommand` parametrizado y `ExecuteReaderAsync()` en `EstadisticasAdoRepositorio.ObtenerResumenAsync` para las métricas del encabezado.
  - **Modo desconectado:** Carga de un `DataTable` mediante `DataTable.Load(reader)` en `ObtenerTablaAuditoriaAsync` enlazado a un `DataGrid` desplegable filtrable por estado.
- **Arquitectura desacoplada (DIP + Repository + SRP):**
  - `MainViewModel` opera exclusivamente con interfaces (`ITareaRepositorio`, `ICategoriaRepositorio`, `IEstadisticasRepositorio`, `ITareaValidador`) y comandos `[RelayCommand]` 100% asíncronos.
  - Dobles en memoria (`TareaRepositorioMemoria`, `CategoriaRepositorioMemoria`, `EstadisticasMemoriaRepositorio`) para probar el `MainViewModel` sin tocar SQLite.

---

## Tecnologías y paquetes

- **Framework:** `.NET 8.0` (`net8.0-windows`, WPF)
- **MVVM:** `CommunityToolkit.Mvvm` (`8.4.2`)
- **ORM y Base de Datos:** `Microsoft.EntityFrameworkCore.Sqlite` (`8.0.11`)
- **Contenedor DI:** `Microsoft.Extensions.DependencyInjection` (`10.0.12`)
- **Testing:** `xUnit` en `MiniKanban.Tests`

---

## Estructura de la solución

```text
MiniKanban.WPF/
├── MiniKanban.WPF.sln
├── MiniKanban.WPF/
│   ├── Data/
│   │   └── AppDbContext.cs                      # DbContext EF Core + HasData + DeleteBehavior.Restrict
│   ├── Models/
│   │   ├── Categoria.cs                         # Entidad Categoría (1:N con Tarea)
│   │   ├── ResumenKanban.cs                     # Record de métricas agregadas ADO.NET
│   │   └── Tarea.cs                             # Entidad Tarea + EstadoTarea + PrioridadTarea
│   ├── Repositories/
│   │   ├── Interfaces/
│   │   │   ├── ITareaRepositorio.cs                 # Contrato Repository de tareas
│   │   │   ├── ICategoriaRepositorio.cs             # Contrato Repository de categorías
│   │   │   └── IEstadisticasRepositorio.cs          # Contrato de métricas y DataTable
│   │   ├── EfCore/
│   │   │   ├── TareaRepositorioEfCore.cs            # Implementación EF Core (LINQ + AsNoTracking + Change Tracker)
│   │   │   └── CategoriaRepositorioEfCore.cs        # Implementación EF Core de categorías
│   │   └── AdoNet/
│   │       └── EstadisticasAdoRepositorio.cs        # Implementación ADO.NET (DataReader + DataTable)
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   └── ITareaValidador.cs                   # Contrato de validación de dominio (SRP)
│   │   └── TareaValidador.cs                        # Reglas de validación de tareas
│   ├── ViewModels/
│   │   └── MainViewModel.cs                         # Lógica de presentación asíncrona y comandos MVVM
│   ├── Views/
│   │   ├── MainWindow.xaml                          # UI WPF + plantilla compartida de tarjeta + DataGrid ADO.NET
│   │   └── MainWindow.xaml.cs                       # Inicialización con DI y carga asíncrona
│   └── App.xaml / App.xaml.cs                       # Contenedor DI + EnsureCreated()
└── MiniKanban.Tests/
    ├── Fakes/
    │   ├── TareaRepositorioMemoria.cs               # Doble en memoria (DIP / tests)
    │   ├── CategoriaRepositorioMemoria.cs           # Doble en memoria (DIP / tests)
    │   └── EstadisticasMemoriaRepositorio.cs        # Doble en memoria (DIP / tests)
    ├── MainViewModelTests.cs                        # Pruebas unitarias en memoria del ViewModel (DIP)
    └── SqlitePersistenciaTests.cs                   # Pruebas de integración con SQLite (EF Core + ADO.NET)
```

Consulta la explicación detallada de cada clase, interfaz, método, propiedad, ciclo y decisión arquitectónica en [EXPLICACION_PROYECTO.md](EXPLICACION_PROYECTO.md).

---

## Ejecución y pruebas

1. Clonar el repositorio y abrir `MiniKanban.WPF.sln` en Visual Studio 2022, o usar la CLI de .NET:
   ```bash
   dotnet build MiniKanban.WPF.sln
   dotnet test MiniKanban.WPF.sln
   dotnet run --project MiniKanban.WPF/MiniKanban.WPF.csproj
   ```
2. Al iniciar la aplicación, `EnsureCreated()` creará automáticamente `kanban.db` en la carpeta de ejecución con las categorías y tareas iniciales.
