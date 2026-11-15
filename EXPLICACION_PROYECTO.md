# Explicación Completa del Proyecto `MiniKanban.WPF`

Guía de estudio y referencia técnica de **cada archivo, clase, interfaz, enum, record, propiedad, método, variable, ciclo y decisión de diseño** implementada en la solución.

---

## 1. Visión general de la arquitectura

El proyecto implementa un **Tablero Kanban de escritorio en WPF (.NET 8)** combinando:

1. **MVVM (`CommunityToolkit.Mvvm`):** la vista ([MainWindow.xaml](MiniKanban.WPF/Views/MainWindow.xaml)) solo enlaza datos y comandos (`Binding`), el ViewModel ([MainViewModel.cs](MiniKanban.WPF/ViewModels/MainViewModel.cs)) coordina la UI de forma reactiva y asíncrona, y los modelos/repositorios manejan dominio y datos.
2. **DIP (Principio de Inversión de Dependencias) + Patrón Repository:** `MainViewModel` depende exclusivamente de interfaces (`ITareaRepositorio`, `ICategoriaRepositorio`, `IEstadisticasRepositorio`, `ITareaValidador`) inyectadas por constructor en [App.xaml.cs](MiniKanban.WPF/App.xaml.cs). Esto permite usar **SQLite** en ejecución real y **repositorios en memoria** en pruebas unitarias sin tocar una sola línea del ViewModel.
3. **EF Core + SQLite (`EnsureCreated`) y LINQ:** persistencia Code-First con entidades anotadas, relación `1:N`, carga de navegación con `Include()`, lecturas optimizadas con `AsNoTracking()`, rastreo con el **Change Tracker** para escrituras, captura de `DbUpdateException` y consultas LINQ con ejecución diferida.
4. **ADO.NET puro (conectado y desconectado):** métricas agregadas con `SqliteConnection`, `SqliteCommand` parametrizado y `ExecuteReaderAsync()`, además de una tabla de auditoría cargada en un `DataTable` enlazado a un `DataGrid`.
5. **SRP (Responsabilidad Única):** las reglas de validación se aislaron en `TareaValidador` para no mezclar lógica de negocio dentro de `MainViewModel`.

---

## 2. Configuración del proyecto y arranque

### 2.1 [MiniKanban.WPF.csproj](MiniKanban.WPF/MiniKanban.WPF.csproj)
Define la aplicación de escritorio WPF (`OutputType=WinExe`, `TargetFramework=net8.0-windows`, `Nullable=enable`, `ImplicitUsings=enable`, `UseWPF=true`) y los paquetes NuGet:
- `CommunityToolkit.Mvvm` `8.4.2`: genera código MVVM en tiempo de compilación (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- `Microsoft.EntityFrameworkCore.Sqlite` `8.0.11`: proveedor EF Core para SQLite (incluye `Microsoft.Data.Sqlite` usado también en ADO.NET).
- `Microsoft.Extensions.DependencyInjection` `10.0.12`: contenedor IoC para resolver dependencias automáticamente.

### 2.2 [App.xaml](MiniKanban.WPF/App.xaml)
Solo conserva la declaración `<Application x:Class="MiniKanban.WPF.App" ...>`.
- **Por qué se quitó `StartupUri`:** con `StartupUri="MainWindow.xaml"`, WPF crea la ventana usando un constructor sin parámetros, lo que impediría inyectarle `MainViewModel` por constructor. Al quitar `StartupUri`, el control pasa a `OnStartup` en [App.xaml.cs](MiniKanban.WPF/App.xaml.cs).

### 2.3 [App.xaml.cs](MiniKanban.WPF/App.xaml.cs)
Contiene la clase `public partial class App : Application` (punto de arranque y composición de dependencias).

- **Campo privado:**
  - `private ServiceProvider? _serviceProvider;` — guarda el contenedor DI construido al iniciar para poder liberarlo (`Dispose()`) cuando se cierre la aplicación.
- **Métodos:**
  - `OnStartup(StartupEventArgs e)`:
    1. Crea `var services = new ServiceCollection();`.
    2. Llama a `ConfigurarServicios(services);` para registrar todas las dependencias.
    3. Construye `_serviceProvider = services.BuildServiceProvider();`.
    4. Llama a `InicializarBaseDeDatos(_serviceProvider);` para asegurar que la base de datos SQLite exista antes de mostrar la ventana.
    5. Resuelve `var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();` (el contenedor crea automáticamente `MainViewModel` con sus 4 interfaces e inyecta ese ViewModel en `MainWindow`) y llama a `mainWindow.Show();`.
  - `OnExit(ExitEventArgs e)`: llama `_serviceProvider?.Dispose();` para liberar los recursos registrados antes de cerrar el proceso.
  - `ConfigurarServicios(IServiceCollection services)`:
    - Variables locales:
      - `string rutaDb = Path.Combine(AppContext.BaseDirectory, "kanban.db");` — ubica `kanban.db` junto al ejecutable para que la app sea portable.
      - `string connectionString = $"Data Source={rutaDb}";` — cadena de conexión SQLite compartida entre EF Core y ADO.NET.
    - Registros DI:
      - `AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString))`: registra una **fábrica** de contextos en lugar de un único `DbContext` singleton. **Por qué:** en WPF con operaciones `async`, cada operación del repositorio crea un `AppDbContext` breve (`await using var db = ...`) y evita conflictos de concurrencia sobre un mismo contexto.
      - `AddSingleton<ITareaValidador, TareaValidador>()`: una única instancia sin estado para validar tareas.
      - `AddSingleton<ITareaRepositorio, TareaRepositorioEfCore>()` y `AddSingleton<ICategoriaRepositorio, CategoriaRepositorioEfCore>()`: conectan los contratos del repositorio con EF Core (cambiar estas dos líneas por `...Memoria` cambiaría todo el almacenamiento sin tocar `MainViewModel`).
      - `AddSingleton<IEstadisticasRepositorio>(_ => new EstadisticasAdoRepositorio(connectionString))`: instancia el repositorio ADO.NET pasándole la cadena de conexión SQLite.
      - `AddTransient<MainViewModel>()` y `AddTransient<MainWindow>()`: se crean por demanda para la ventana principal.
  - `InicializarBaseDeDatos(IServiceProvider serviceProvider)`:
    - Variables: `var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();` y `using var db = factory.CreateDbContext();`.
    - **Condicional `if (db.Database.EnsureCreated())`:** crea el archivo `kanban.db`, las tablas (`Categorias`, `Tareas`), sus claves foráneas y las 4 categorías semilla configuradas con `HasData(...)` si la base de datos aún no existía.
    - Cuando `EnsureCreated()` devuelve `true` (solo la primera vez que nace la BD), inserta 3 tareas iniciales con `db.Tareas.AddRange(...)` y `db.SaveChanges()`:
      1. `"Preparar presentación INF033"` (`PrioridadTarea.Alta`, `EstadoTarea.EnProgreso`, `CategoriaId = 1`).
      2. `"Revisar pruebas unitarias (DIP)"` (`PrioridadTarea.Media`, `EstadoTarea.PorHacer`, `CategoriaId = 1`).
      3. `"Configurar SQLite con EnsureCreated"` (`PrioridadTarea.Baja`, `EstadoTarea.Hecho`, `CategoriaId = 2`).

### 2.4 [AssemblyInfo.cs](MiniKanban.WPF/AssemblyInfo.cs)
Atributo estándar `[assembly: ThemeInfo(...)]` generado por WPF indicando dónde buscar estilos de tema genéricos del ensamblado.

---

## 3. Capa de Modelos (`Models/`)

### 3.1 [Tarea.cs](MiniKanban.WPF/Models/Tarea.cs)
- **Enum `EstadoTarea`:** `PorHacer`, `EnProgreso`, `Hecho`. Representa las 3 columnas del tablero Kanban. Se guarda en SQLite como entero (`0`, `1`, `2`).
- **Enum `PrioridadTarea`:** `Baja`, `Media`, `Alta`. Clasifica la urgencia (`0`, `1`, `2`).
- **Clase `Tarea`:** entidad principal persistida en la tabla `Tareas` de SQLite.
  - `Id` (`int`, `[Key]`): clave primaria autoincremental en SQLite.
  - `Titulo` (`string`, `[Required]`, `[MaxLength(120)]`, valor inicial `string.Empty`): genera columna `NOT NULL` con longitud máxima documentada por Data Annotations.
  - `Descripcion` (`string`, `[MaxLength(500)]`, valor inicial `string.Empty`): texto opcional de detalle.
  - `FechaCreacion` (`DateTime`, default `DateTime.Now`): marca de creación.
  - `FechaLimite` (`DateTime`, default `DateTime.Today.AddDays(1)`): fecha compromiso de entrega.
  - `Estado` (`EstadoTarea`, default `EstadoTarea.PorHacer`): columna actual.
  - `Prioridad` (`PrioridadTarea`, default `PrioridadTarea.Media`): prioridad por defecto.
  - `CategoriaId` (`int`): clave foránea explícita hacia `Categoria`.
  - `Categoria` (`Categoria?`): propiedad de navegación hacia la entidad padre para poder mostrar `Categoria.Nombre` y `Categoria.ColorHex` en cada tarjeta.

### 3.2 [Categoria.cs](MiniKanban.WPF/Models/Categoria.cs)
Entidad padre persistida en la tabla `Categorias`.
- `Id` (`int`, `[Key]`): clave primaria.
- `Nombre` (`string`, `[Required]`, `[MaxLength(50)]`, default `string.Empty`): nombre visible de la categoría.
- `ColorHex` (`string`, `[Required]`, `[MaxLength(7)]`, default `"#0052CC"`): código hexadecimal usado directamente por WPF en el fondo de la etiqueta de categoría de cada tarjeta.
- `Tareas` (`ICollection<Tarea>`, inicializada con `new List<Tarea>()`): navegación inversa de la relación `1:N`.
- `ToString()`: devuelve `Nombre` para representación legible en depuración o controles.

### 3.3 [ResumenKanban.cs](MiniKanban.WPF/Models/ResumenKanban.cs)
`public record ResumenKanban(int TotalPorHacer, int TotalEnProgreso, int TotalHechas, int TotalPrioridadAlta, int TotalVencidas);`
- **Por qué `record`:** es inmutable y ligero; sirve como DTO de proyección para transportar los 5 contadores calculados por ADO.NET hacia las pastillas de resumen del encabezado.

---

## 4. Capa de Datos EF Core (`Data/`)

### 4.1 [AppDbContext.cs](MiniKanban.WPF/Data/AppDbContext.cs)
Clase `public class AppDbContext : DbContext` que representa una unidad de trabajo/sesión contra SQLite.
- **`DbSet<Tarea> Tareas => Set<Tarea>();`** y **`DbSet<Categoria> Categorias => Set<Categoria>();`**: mapean ambas entidades a tablas consultables mediante LINQ y gestionadas por el Change Tracker.
- **Constructor `AppDbContext(DbContextOptions<AppDbContext> options) : base(options)`:** recibe la configuración (cadena SQLite) desde el contenedor DI o desde los tests.
- **`OnConfiguring(DbContextOptionsBuilder optionsBuilder)`:** respaldo defensivo (`if (!optionsBuilder.IsConfigured) optionsBuilder.UseSqlite("Data Source=kanban.db");`) por si alguien instancia `AppDbContext` sin pasar opciones configuradas.
- **`OnModelCreating(ModelBuilder modelBuilder)`:**
  - Configura por Fluent API la relación `1:N`: `Tarea` tiene una `Categoria` (`HasOne`), `Categoria` tiene muchas `Tareas` (`WithMany`), enlazadas por `CategoriaId` (`HasForeignKey`), con **`OnDelete(DeleteBehavior.Restrict)`** (impide borrar una categoría en SQLite si todavía tiene tareas asociadas, protegiendo la integridad referencial).
  - Configura **datos semilla** con `modelBuilder.Entity<Categoria>().HasData(...)`:
    - Id `1`: `"Universidad"` (`#0052CC`)
    - Id `2`: `"Trabajo"` (`#00875A`)
    - Id `3`: `"Personal"` (`#FF8B00`)
    - Id `4`: `"General"` (`#5E6C84`)

---

## 5. Capa de Validación / Servicios (`Services/`)

### 5.1 [ITareaValidador.cs](MiniKanban.WPF/Services/ITareaValidador.cs)
Interfaz `public interface ITareaValidador` con un único método `void Validar(Tarea tarea);`.
- **Por qué existe:** aplica **SRP** sacando reglas de negocio fuera de `MainViewModel` y **DIP** permitiendo inyectar o sustituir la validación.

### 5.2 [TareaValidador.cs](MiniKanban.WPF/Services/TareaValidador.cs)
Implementación `public class TareaValidador : ITareaValidador`:
- **`Validar(Tarea tarea)`:** lanza excepciones descriptivas ante cualquier incumplimiento (luego capturadas por `MainViewModel` para mostrarlas en `MensajeError`):
  1. `ArgumentNullException.ThrowIfNull(tarea);` — evita referencias nulas.
  2. `string.IsNullOrWhiteSpace(tarea.Titulo)` -> `ArgumentException("El título de la tarea es obligatorio.")`.
  3. `tarea.Titulo.Trim().Length < 3` -> `ArgumentException("El título debe tener al menos 3 caracteres.")`.
  4. `tarea.Titulo.Length > 120` -> `ArgumentException("El título no puede superar los 120 caracteres.")`.
  5. `tarea.Descripcion.Length > 500` -> `ArgumentException("La descripción no puede superar los 500 caracteres.")`.
  6. `tarea.CategoriaId <= 0` -> `ArgumentException("Debe seleccionar una categoría válida.")`.
  7. `tarea.FechaLimite.Date < DateTime.Today` -> `ArgumentException("La fecha límite no puede ser anterior a hoy.")`.

---

## 6. Capa de Repositorios (`Repositories/`)

### 6.1 Contratos (Interfaces para DIP)

1. **[ITareaRepositorio.cs](MiniKanban.WPF/Repositories/ITareaRepositorio.cs)** (`ITareaRepositorio`):
   - `ObtenerFiltradasAsync(string? textoBusqueda = null, PrioridadTarea? prioridad = null, int? categoriaId = null, bool ordenarPorFechaLimite = true)` -> `Task<List<Tarea>>`: obtiene tareas aplicando filtros y orden.
   - `ObtenerPorIdAsync(int id)` -> `Task<Tarea?>`: busca una tarea puntual por ID.
   - `AgregarAsync(Tarea tarea)` -> `Task`: persiste una nueva tarea.
   - `CambiarEstadoAsync(int id, EstadoTarea nuevoEstado)` -> `Task`: actualiza la columna (`Estado`) de una tarea.
   - `EliminarAsync(int id)` -> `Task`: elimina una tarea por ID.
2. **[ICategoriaRepositorio.cs](MiniKanban.WPF/Repositories/ICategoriaRepositorio.cs)** (`ICategoriaRepositorio`):
   - `ObtenerTodasAsync()` -> `Task<List<Categoria>>`: lista las categorías disponibles.
   - `AgregarAsync(Categoria categoria)` -> `Task`: registra una nueva categoría.
3. **[IEstadisticasRepositorio.cs](MiniKanban.WPF/Repositories/IEstadisticasRepositorio.cs)** (`IEstadisticasRepositorio`):
   - `ObtenerResumenAsync(DateTime fechaReferencia)` -> `Task<ResumenKanban>`: devuelve los contadores globales del tablero usando `fechaReferencia` para evaluar tareas vencidas.
   - `ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null)` -> `Task<DataTable>`: devuelve una tabla en memoria para auditoría SQL.

### 6.2 Implementaciones EF Core + SQLite

#### [TareaRepositorioEfCore.cs](MiniKanban.WPF/Repositories/TareaRepositorioEfCore.cs)
- **Campo privado:** `private readonly IDbContextFactory<AppDbContext> _contextFactory;`
- **Métodos:**
  - **`ObtenerFiltradasAsync(...)`:**
    1. Crea el contexto breve: `await using var db = await _contextFactory.CreateDbContextAsync();`.
    2. Construye una consulta **diferida** `IQueryable<Tarea> consulta = db.Tareas.AsNoTracking().Include(t => t.Categoria);`.
       - `AsNoTracking()` desactiva el Change Tracker en lecturas para ahorrar memoria y CPU.
       - `Include(t => t.Categoria)` hace un `JOIN` en SQL para traer la categoría junto con cada tarea.
    3. Encadena filtros LINQ dinámicamente **antes de ejecutar SQL**:
       - Si `textoBusqueda` no está vacío: arma `string patron = $"%{textoBusqueda.Trim()}%";` y filtra con `EF.Functions.Like(t.Titulo, patron) || EF.Functions.Like(t.Descripcion, patron)`.
       - Si `prioridad.HasValue`: filtra `t.Prioridad == prioridad.Value`.
       - Si `categoriaId.HasValue && categoriaId.Value > 0`: filtra `t.CategoriaId == categoriaId.Value`.
    4. Ordena con ternario según `ordenarPorFechaLimite`:
       - `true`: `OrderBy(t => t.FechaLimite).ThenByDescending(t => t.Prioridad)`.
       - `false`: `OrderByDescending(t => t.Prioridad).ThenByDescending(t => t.Id)`.
    5. **Ejecución materializada:** `return await consulta.ToListAsync();` traduce toda la expresión LINQ en una única sentencia `SELECT` en SQLite.
  - **`ObtenerPorIdAsync(int id)`:** usa `.AsNoTracking().Include(t => t.Categoria).FirstOrDefaultAsync(t => t.Id == id)`.
  - **`AgregarAsync(Tarea tarea)`:**
    - Valida no nulo (`ArgumentNullException.ThrowIfNull(tarea)`).
    - **Detalle clave:** asigna `tarea.Categoria = null;` para que EF Core use únicamente `CategoriaId` como FK y no intente insertar una categoría duplicada.
    - `db.Tareas.Add(tarea);` marca la entidad en estado `Added` en el Change Tracker y llama a `GuardarCambiosAsync(db)` para ejecutar `INSERT`.
  - **`CambiarEstadoAsync(int id, EstadoTarea nuevoEstado)`:**
    - Busca con `var tarea = await db.Tareas.FindAsync(id);` (**aquí sí con tracking activo**).
    - Si `tarea is null || tarea.Estado == nuevoEstado`, retorna temprano sin escribir en disco.
    - Al cambiar `tarea.Estado = nuevoEstado;`, el Change Tracker detecta el cambio, marca la entidad como `Modified` y `GuardarCambiosAsync(db)` genera el `UPDATE` solo de esa columna.
  - **`EliminarAsync(int id)`:** busca con `FindAsync(id)`; si existe, `db.Tareas.Remove(tarea);` la marca como `Deleted` y `GuardarCambiosAsync(db)` ejecuta `DELETE`.
  - **`GuardarCambiosAsync(AppDbContext db)` (helper privado estático):** envuelve `await db.SaveChangesAsync();` en `try/catch (DbUpdateException ex)` y lo relanza como `InvalidOperationException` con mensaje legible si falla una restricción (por ejemplo FK inválida).

#### [CategoriaRepositorioEfCore.cs](MiniKanban.WPF/Repositories/CategoriaRepositorioEfCore.cs)
- **Campo privado:** `private readonly IDbContextFactory<AppDbContext> _contextFactory;`
- **`ObtenerTodasAsync()`:** consulta `db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync()`.
- **`AgregarAsync(Categoria categoria)`:** verifica no nulo, hace `db.Categorias.Add(categoria)` y guarda capturando `DbUpdateException`.

### 6.3 Implementación ADO.NET puro

#### [EstadisticasAdoRepositorio.cs](MiniKanban.WPF/Repositories/EstadisticasAdoRepositorio.cs)
Demuestra uso directo de `Microsoft.Data.Sqlite` sin EF Core.
- **Campo privado:** `private readonly string _connectionString;`
- **`ObtenerResumenAsync(DateTime fechaReferencia)` (Modo conectado):**
  1. Abre `await using var conn = new SqliteConnection(_connectionString);` y `await conn.OpenAsync();`.
  2. Crea `await using var cmd = conn.CreateCommand();` con un `SELECT` agregado usando `COALESCE(SUM(CASE WHEN ...), 0)` para calcular en una sola pasada SQL:
     - Total `Por Hacer` (`@porHacer`)
     - Total `En Progreso` (`@enProgreso`)
     - Total `Hechas` (`@hecho`)
     - Total `Prioridad Alta` pendientes (`Prioridad = @alta AND Estado <> @hecho`)
     - Total `Vencidas` pendientes (`date(FechaLimite) < date(@fechaRef) AND Estado <> @hecho`)
  3. Añade parámetros con `cmd.Parameters.AddWithValue(...)` evitando inyección SQL (`@porHacer`, `@enProgreso`, `@hecho`, `@alta`, `@fechaRef`).
  4. Lee fila a fila en forward-only con `await using var reader = await cmd.ExecuteReaderAsync();` y retorna `new ResumenKanban(reader.GetInt32(0), ...)` (o ceros si no hay lectura).
- **`ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null)` (Modo desconectado):**
  1. Instancia `var tabla = new DataTable("AuditoriaTareas");`.
  2. Arma la consulta `sql` con `INNER JOIN Categorias c ON t.CategoriaId = c.Id` y `CASE` / `strftime('%Y-%m-%d', t.FechaLimite)` para formatear estado, prioridad y fecha legibles.
  3. Si `estadoFiltro.HasValue`, concatena `" WHERE t.Estado = @estado"` y agrega el parámetro `@estado`.
  4. Ordena con `" ORDER BY t.Id DESC;"`, ejecuta `ExecuteReaderAsync()` y carga todo el resultado en memoria con `tabla.Load(reader);` antes de cerrar la conexión. Así la vista enlaza directamente `TablaAuditoria.DefaultView` a un `DataGrid`.

### 6.4 Implementaciones en Memoria (Dobles para Pruebas y DIP)

1. **[TareaRepositorioMemoria.cs](MiniKanban.WPF/Repositories/TareaRepositorioMemoria.cs):**
   - **Campos:**
     - `private readonly List<Tarea> _tareas = new();` — almacenamiento en RAM.
     - `private readonly Dictionary<int, Categoria> _categorias;` — diccionario indexado por `Id` para asociar categorías rápido.
     - `private int _siguienteId = 1;` — simula el autoincremental de SQLite.
   - **Constructor:** carga `categoriasIniciales` (o las 4 por defecto: `Universidad`, `Trabajo`, `Personal`, `General`) convertidas a diccionario con `.ToDictionary(c => c.Id)`.
   - **Métodos:** replican con LINQ en memoria exactamente el comportamiento de `TareaRepositorioEfCore` (búsqueda case-insensitive con `StringComparison.OrdinalIgnoreCase`, filtros por prioridad/categoría, ordenamiento y validación de existencia de `CategoriaId` en `AgregarAsync`).
   - **Helper `ClonarConCategoria(Tarea origen)`:** busca la categoría con `_categorias.TryGetValue(...)` y devuelve una **copia nueva** de cada `Tarea` al consultar o agregar, simulando el comportamiento desacoplado de `AsNoTracking()` (modificar un objeto retornado en memoria no altera la lista interna del repositorio).
2. **[CategoriaRepositorioMemoria.cs](MiniKanban.WPF/Repositories/CategoriaRepositorioMemoria.cs):**
   - **Campos:** `private readonly List<Categoria> _categorias;` y `private int _siguienteId;` (calculado con `_categorias.Count == 0 ? 1 : _categorias.Max(c => c.Id) + 1`).
   - **Métodos:** `ObtenerTodasAsync()` (ordenadas por `Nombre`) y `AgregarAsync(Categoria)` asignando `_siguienteId++`.
3. **[EstadisticasMemoriaRepositorio.cs](MiniKanban.WPF/Repositories/EstadisticasMemoriaRepositorio.cs):**
   - **Campo:** `private readonly ITareaRepositorio _tareaRepositorio;`.
   - **`ObtenerResumenAsync(DateTime fechaReferencia)`:** usa LINQ `.Count(...)` replicando exactamente los mismos criterios que la consulta SQL de ADO.NET.
   - **`ObtenerTablaAuditoriaAsync(EstadoTarea? estadoFiltro = null)`:** crea un `DataTable("AuditoriaTareas")` con columnas tipadas (`ID`, `Titulo`, `Categoria`, `Estado`, `Prioridad`, `FechaLimite`), ordena `filtradas.OrderByDescending(x => x.Id)` y ejecuta el **ciclo `foreach`**:
     - `foreach (var t in filtradas.OrderByDescending(x => x.Id))` — recorre cada tarea filtrada y agrega su fila con `tabla.Rows.Add(...)`.

---

## 7. ViewModel y Records Auxiliares (`ViewModels/`)

### 7.1 [MainViewModel.cs](MiniKanban.WPF/ViewModels/MainViewModel.cs)

#### Records auxiliares para ComboBoxes
- `public record FiltroPrioridadOpcion(string Etiqueta, PrioridadTarea? Valor);` — representa `"Todas las prioridades"` (`null`) o una prioridad concreta.
- `public record FiltroEstadoOpcion(string Etiqueta, EstadoTarea? Valor);` — representa `"Todos los estados"` (`null`) o un `EstadoTarea` concreto para el filtro de auditoría ADO.NET.

#### Dependencias inyectadas (campos `private readonly`)
- `_tareaRepositorio` (`ITareaRepositorio`)
- `_categoriaRepositorio` (`ICategoriaRepositorio`)
- `_estadisticasRepositorio` (`IEstadisticasRepositorio`)
- `_validador` (`ITareaValidador`)

#### Propiedades observables (`[ObservableProperty]`)
`CommunityToolkit.Mvvm` genera automáticamente la propiedad pública (ej. `_nuevoTitulo` -> `NuevoTitulo`) junto con `OnPropertyChanged`:
- **Formulario de creación:**
  - `NuevoTitulo` (`string`, `""`)
  - `NuevaDescripcion` (`string`, `""`)
  - `NuevaPrioridad` (`PrioridadTarea`, `PrioridadTarea.Media`)
  - `NuevaFechaLimite` (`DateTime`, `DateTime.Today.AddDays(1)`)
  - `CategoriaSeleccionada` (`Categoria?`, `null`)
- **Filtros LINQ y Auditoría ADO.NET:**
  - `TextoBusqueda` (`string`, `""`)
  - `FiltroPrioridadSeleccionada` (`FiltroPrioridadOpcion`)
  - `CategoriaFiltroSeleccionada` (`Categoria`)
  - `OrdenarPorFechaLimite` (`bool`, `true`)
  - `FiltroEstadoAuditoria` (`FiltroEstadoOpcion`)
- **Estado de interfaz y reportes:**
  - `MensajeError` (`string`, `""`) — muestra errores de validación o BD en el banner rojo.
  - `MensajeEstado` (`string`, `""`) — muestra `"Mostrando X tarea(s)"`.
  - `EstaCargando` (`bool`, `false`) — indica operación asíncrona en curso.
  - `Resumen` (`ResumenKanban`, `new(0, 0, 0, 0, 0)`) — alimenta las 5 pastillas superiores.
  - `TablaAuditoria` (`DataTable`, `new()`) — alimenta el `DataGrid` SQL.
  - `MostrarAuditoriaAdo` (`bool`, `false`) — controla si el panel inferior ADO.NET está visible.

#### Colecciones enlazadas a la Vista
- `Categorias` (`ObservableCollection<Categoria>`): opciones para crear tarea.
- `CategoriasFiltro` (`ObservableCollection<Categoria>`): incluye `"Todas las categorías"` (`Id = 0`) + las categorías reales.
- `TareasPorHacer`, `TareasEnProgreso`, `TareasHechas` (`ObservableCollection<Tarea>`): las 3 columnas del tablero.
- `PrioridadesDisponibles` (`IReadOnlyList<PrioridadTarea>`): `Enum.GetValues<PrioridadTarea>()`.
- `FiltrosPrioridad` (`IReadOnlyList<FiltroPrioridadOpcion>`): 4 opciones inicializadas en el constructor.
- `FiltrosEstadoAuditoria` (`IReadOnlyList<FiltroEstadoOpcion>`): 4 opciones inicializadas en el constructor.

#### Comandos asíncronos (`[RelayCommand]`) y métodos privados
Cada método decorado con `[RelayCommand]` genera automáticamente una propiedad `IAsyncRelayCommand` (ej. `InicializarAsync` -> `InicializarCommand`):

1. **`InicializarAsync` (`InicializarCommand`):**
   - Obtiene las categorías desde `_categoriaRepositorio.ObtenerTodasAsync()`.
   - Limpia `Categorias` y `CategoriasFiltro`, agrega `opcionTodas` (`Id = 0`) y ejecuta el **ciclo `foreach`**:
     - `foreach (var categoria in categorias)` — inserta cada categoría en ambas colecciones (`Categorias` para el formulario y `CategoriasFiltro` para el filtro).
   - Selecciona la primera categoría real por defecto (`CategoriaSeleccionada ??= Categorias.FirstOrDefault();`), selecciona `opcionTodas` en el filtro y llama a `RecargarDatosInternoAsync()`.
2. **`CargarTableroAsync` (`CargarTableroCommand`):** recarga columnas, resumen y auditoría aplicando los filtros actuales.
3. **`LimpiarFiltrosAsync` (`LimpiarFiltrosCommand`):** reinicia `TextoBusqueda = ""`, `FiltroPrioridadSeleccionada = FiltrosPrioridad[0]`, `CategoriaFiltroSeleccionada = CategoriasFiltro[0]`, `OrdenarPorFechaLimite = true` y recarga todo el tablero.
4. **`CrearTareaAsync` (`CrearTareaCommand`):**
   - Construye `var nuevaTarea = new Tarea { ... }` recortando espacios (`Trim()`), con `Estado = EstadoTarea.PorHacer` y `CategoriaId = CategoriaSeleccionada?.Id ?? 0`.
   - Llama `_validador.Validar(nuevaTarea);` y luego `await _tareaRepositorio.AgregarAsync(nuevaTarea);`.
   - Limpia los campos del formulario y recarga el tablero.
5. **`AvanzarTareaAsync` (`AvanzarTareaCommand`)** y **`RetrocederTareaAsync` (`RetrocederTareaCommand`):**
   - Delegan al helper común **`CambiarEstadoConTransicionAsync(Tarea? tarea, Func<EstadoTarea, EstadoTarea?> calcularSiguienteEstado)`**:
     - Evita duplicar código entre avanzar (`PorHacer -> EnProgreso -> Hecho`) y retroceder (`Hecho -> EnProgreso -> PorHacer`).
     - Si `tarea` es `null` o no tiene transición válida (`_ => null`), retorna sin ir a la base de datos.
6. **`EliminarTareaAsync` (`EliminarTareaCommand`):** elimina la tarea por `tarea.Id` y recarga el tablero.
7. **`ToggleAuditoriaAdoAsync` (`ToggleAuditoriaAdoCommand`):** invierte `MostrarAuditoriaAdo`; si queda en `true`, consulta inmediatamente el `DataTable` ADO.NET.
8. **`ActualizarAuditoriaAdoAsync` (`ActualizarAuditoriaAdoCommand`):** ejecuta `ActualizarAuditoriaInternoAsync()` al presionar `"Consultar SQL"`.
9. **Helpers internos:**
   - **`RecargarDatosInternoAsync()`:** calcula `int? categoriaIdFiltro = CategoriaFiltroSeleccionada.Id > 0 ? CategoriaFiltroSeleccionada.Id : null;`, pide `tareas` filtradas por LINQ al repositorio, llama a `DistribuirEnColumnas(tareas)`, actualiza `Resumen` desde `_estadisticasRepositorio.ObtenerResumenAsync(DateTime.Today)`, actualiza `MensajeEstado`, y si `MostrarAuditoriaAdo` está abierto refresca también `TablaAuditoria`.
   - **`DistribuirEnColumnas(IEnumerable<Tarea> tareas)`:** vacía las 3 `ObservableCollection` y ejecuta el **ciclo `foreach`**:
     - `foreach (var tarea in tareas)` — clasifica cada tarea con una expresión `switch` sobre `tarea.Estado` y la agrega a `TareasPorHacer`, `TareasEnProgreso` o `TareasHechas`.
   - **`EjecutarConManejoDeErrorAsync(Func<Task> accion)`:** centraliza el bloque `try / catch / finally`: activa `EstaCargando = true`, limpia `MensajeError`, captura `ArgumentException` (validaciones), `InvalidOperationException` (errores de BD/EF Core) o cualquier `Exception` inesperada mostrando el texto en `MensajeError`, y apaga `EstaCargando = false` en `finally`.

---

## 8. Capa de Vista (`Views/`)

### 8.1 [MainWindow.xaml](MiniKanban.WPF/Views/MainWindow.xaml)
- **`Window.Resources`:**
  - `BooleanToVisibilityConverter` (`x:Key="BoolToVis"`): convierte `MostrarAuditoriaAdo` (`bool`) en `Visible`/`Collapsed` para mostrar u ocultar el panel inferior de ADO.NET.
  - `Style` `ColumnaKanbanStyle` (`x:Key="ColumnaKanbanStyle"`): estilo reutilizado en los 3 `Border` de las columnas (`#EBECF0`, bordes redondeados).
  - `Style` `ResumenPillStyle` (`x:Key="ResumenPillStyle"`): estilo reutilizado en las 5 pastillas de estadísticas.
  - **`DataTemplate` compartida `TarjetaKanbanTemplate` (`DataType="{x:Type models:Tarea}"`):** evita duplicar XAML en las 3 columnas. Cada tarjeta enlaza:
    - Etiqueta de categoría con `Background="{Binding Categoria.ColorHex, TargetNullValue=#5E6C84}"` y `Text="{Binding Categoria.Nombre, TargetNullValue=General}"`.
    - Badge de prioridad con `DataTrigger`s de color (verde `Baja`, amarillo `Media`, rojo `Alta`).
    - Título con `DataTrigger`: cuando `Estado == Hecho` aplica `TextDecorations="Strikethrough"`.
    - Botones `Retroceder`, `Eliminar` y `Avanzar` usando `RelativeSource AncestorType=Window` para acceder a los comandos del `MainViewModel` (`DataContext`) pasándoles la propia tarjeta en `CommandParameter="{Binding}"`, y ocultando `Retroceder` en `PorHacer` o `Avanzar` en `Hecho` (y cambiando el texto `"Avanzar ➡️"` a `"Completar ➡️"` en `EnProgreso`).
- **Distribución en 6 filas (`Grid.RowDefinitions`):**
  - Fila 0: Título + 5 pastillas de métricas (`Resumen.*`) + botón `"📊 Auditoría SQL (ADO.NET)"`.
  - Fila 1: Banner de error conectado a `MensajeError` (oculto con `DataTrigger` cuando está vacío `""`).
  - Fila 2: Formulario de creación (`NuevoTitulo`, `NuevaDescripcion`, `CategoriaSeleccionada`, `NuevaPrioridad`, `NuevaFechaLimite`, `CrearTareaCommand`).
  - Fila 3: Barra de filtros LINQ (`TextoBusqueda` con `KeyBinding Return`, `FiltrosPrioridad`, `CategoriasFiltro`, `CheckBox OrdenarPorFechaLimite`, botones `"Filtrar"` y `"Limpiar"`) y contador `MensajeEstado`.
  - Fila 4: Las 3 columnas Kanban (`TareasPorHacer`, `TareasEnProgreso`, `TareasHechas`) consumiendo `TarjetaKanbanTemplate`.
  - Fila 5: Panel desplegable de auditoría ADO.NET con `ComboBox FiltrosEstadoAuditoria`, botón `ActualizarAuditoriaAdoCommand` y `DataGrid ItemsSource="{Binding TablaAuditoria.DefaultView}"`.

### 8.2 [MainWindow.xaml.cs](MiniKanban.WPF/Views/MainWindow.xaml.cs)
Code-behind mínimo (sin lógica de negocio ni acceso a datos):
- Recibe `MainViewModel viewModel` por constructor (inyectado desde DI).
- Guarda `private readonly MainViewModel _viewModel;`, asigna `DataContext = _viewModel;` y se suscribe a `Loaded += MainWindow_Loaded;`.
- En `MainWindow_Loaded`: se desuscribe (`Loaded -= MainWindow_Loaded;`) para ejecutarse una sola vez y lanza `await _viewModel.InicializarCommand.ExecuteAsync(null);` de forma asíncrona sin bloquear el hilo de interfaz UI.

---

## 9. Proyecto de Pruebas (`MiniKanban.Tests/`)

### 9.1 [MainViewModelTests.cs](MiniKanban.Tests/MainViewModelTests.cs)
Prueba `MainViewModel` **100% en memoria sin tocar SQLite** (demostrando la ventaja práctica de DIP).
- **`CrearSut()` (helper estático):** arma `MainViewModel` con `TareaRepositorioMemoria`, `CategoriaRepositorioMemoria`, `EstadisticasMemoriaRepositorio` y `TareaValidador`.
- **Casos cubiertos:**
  - `InicializarAsync_CargaCategoriasYResumenEnCero`: verifica carga de categorías, opción `"Todas las categorías"` (`Id = 0`) y `ResumenKanban` en cero.
  - `CrearTareaAsync_ConTituloInvalido_MuestraErrorYNoGuarda` (`[Theory]` con `""`, `"   "`, `"AB"`): valida `MensajeError` y que no guarde tareas.
  - `CrearTareaAsync_ConFechaLimitePasada_MuestraErrorYNoGuarda`: comprueba rechazo de fechas pasadas.
  - `CrearTareaAsync_ConDatosValidos_AgregaAPorHacerLimpiaFormularioYActualizaResumen`: verifica inserción, limpieza del formulario y resumen actualizado.
  - `AvanzarYRetrocederTareaAsync_MueveCorrectamenteEntreColumnas`: recorre `PorHacer -> EnProgreso -> Hecho` y de vuelta, además de verificar no-op al intentar avanzar desde `Hecho`.
  - `EliminarTareaAsync_RemueveTareaDelTableroYActualizaResumen`: elimina tarjeta y actualiza métricas.
  - `FiltrosYLimpiarFiltros_FiltranYRestauranTodasLasTareas`: valida búsqueda por descripción + prioridad + reseteo completo con `LimpiarFiltrosCommand`.
  - `AuditoriaAdo_ToggleYEstadoFiltro_GeneraDataTableFiltrada`: valida apertura del panel de auditoría y filtrado por `EstadoTarea.Hecho`.
  - `MainWindow_InicializaXamlYRecursosSinExcepcion`: crea un hilo `STA` (`ApartmentState.STA`), instancia `new MainWindow(vm)`, valida `DataContext` y resolución del recurso XAML `BoolToVis` sin `XamlParseException`, y cierra la ventana.

### 9.2 [SqlitePersistenciaTests.cs](MiniKanban.Tests/SqlitePersistenciaTests.cs)
Pruebas de integración contra una base SQLite real temporal aislada por cada test (`IDisposable` limpia pools con `SqliteConnection.ClearAllPools()` y borra el `.db` temporal en `Dispose()`).
- **`TestDbContextFactory` (clase privada anidada):** implementa `IDbContextFactory<AppDbContext>` usando una ruta temporal única `minikanban_test_<Guid>.db` e invoca `db.Database.EnsureCreated()` en el constructor del test.
- **Casos cubiertos:**
  - `EnsureCreated_CreaEsquemaYSiemillaCategoriasIniciales`: comprueba las 4 categorías semilla ordenadas alfabéticamente.
  - `CategoriaRepositorioEfCore_AgregarAsync_PersisteNuevaCategoria`: prueba inserción real en `Categorias`.
  - `TareaRepositorioEfCore_CrudCompletoYChangeTracker_PersistenEnSqlite`: prueba ciclo `Added -> Include -> Modified -> Deleted`.
  - `TareaRepositorioEfCore_AsNoTracking_NoRastreaEntidadesEnDbContext`: modifica un objeto retornado en memoria y verifica que la BD permanece intacta.
  - `TareaRepositorioEfCore_ObtenerFiltradasAsync_AplicaLinqBusquedaPrioridadCategoriaYOrden`: comprueba filtrado combinado con `EF.Functions.Like`, categoría, prioridad y ordenamiento.
  - `TareaRepositorioEfCore_ConCategoriaInexistente_LanzaInvalidOperationExceptionPorDbUpdateException`: verifica captura de `DbUpdateException` ante violación de clave foránea.
  - `EstadisticasAdoRepositorio_ObtenerResumenYAutoria_ConsultanSqliteConParametrosYDataTable`: prueba tanto `ObtenerResumenAsync` (excluyendo tareas `Hecho` del conteo de altas pendientes y vencidas) como `ObtenerTablaAuditoriaAsync` (con `INNER JOIN` sin filtro y con filtro `EstadoTarea.Hecho`).

---

## 10. ¿Por qué no hay otros `while` o `for` clásicos en el código?

Solo existen **3 ciclos `foreach` explícitos** en toda la solución:
1. `MainViewModel.InicializarAsync` (`foreach (var categoria in categorias)`): pobla las `ObservableCollection` enlazadas a la UI.
2. `MainViewModel.DistribuirEnColumnas` (`foreach (var tarea in tareas)`): reparte las tareas en `TareasPorHacer`, `TareasEnProgreso` y `TareasHechas`.
3. `EstadisticasMemoriaRepositorio.ObtenerTablaAuditoriaAsync` (`foreach (var t in filtradas.OrderByDescending(x => x.Id))`): agrega cada fila al `DataTable` en memoria.

El resto del procesamiento sobre colecciones se realiza declarativamente con **LINQ** (`Where`, `OrderBy`, `Select`, `Count`, `FirstOrDefault`) o directamente dentro del motor **SQLite** mediante SQL (`SUM(CASE WHEN ...)`, `DataTable.Load(reader)`), evitando bucles manuales imperativos y reduciendo la transferencia de datos innecesarios desde la base de datos.

---

## 11. Preguntas típicas para la exposición

1. **¿Por qué usaron `EnsureCreated()` y no `Migrate()`?**
   Porque el proyecto está pensado para iniciar de forma inmediata sin historial de migraciones; si la base `kanban.db` no existe, `EnsureCreated()` crea el esquema completo desde el modelo C# e inserta las categorías semilla de `HasData()`.
2. **¿Por qué combinar EF Core y ADO.NET si EF Core ya consulta SQLite?**
   EF Core agiliza el CRUD de entidades (`Tarea`, `Categoria`) con objetos fuertemente tipados, `Include` y Change Tracker; ADO.NET puro se usa en `EstadisticasAdoRepositorio` para mostrar control SQL directo mediante consultas agregadas parametrizadas (`SqliteDataReader`) y modo desconectado (`DataTable` para `DataGrid`).
3. **¿Cómo demuestran DIP en la práctica?**
   `MainViewModel` nunca instancia `AppDbContext` ni `TareaRepositorioEfCore`: recibe interfaces por constructor. Gracias a eso, `MainViewModelTests` corre sus pruebas sin base de datos usando las implementaciones en memoria.
4. **¿Qué diferencia hay entre `AsNoTracking()` y `FindAsync()` en `TareaRepositorioEfCore`?**
   `ObtenerFiltradasAsync` usa `AsNoTracking()` porque solo lee tarjetas para mostrarlas en pantalla; `CambiarEstadoAsync` y `EliminarAsync` usan `FindAsync()` con tracking activo para que el Change Tracker detecte el estado `Modified` o `Deleted` y genere el `UPDATE`/`DELETE`.
