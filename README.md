# 📋 MiniKanban.WPF

Aplicación de escritorio interactiva estilo **Tablero Kanban** desarrollada con **WPF (.NET 8)** implementando rigurosamente el patrón arquitectónico **MVVM (Model-View-ViewModel)** y buenas prácticas de ingeniería de software (**SOLID**).

---

## 🚀 Características

* **Flujo Kanban Completo:** Gestión de tareas en 3 columnas independientes:
  * 📋 **Por Hacer** (*To Do*)
  * ⏳ **En Progreso** (*In Progress*)
  * ✅ **Hechas** (*Done*, con soporte de tachado visual y reabrir)
* **Operaciones CRUD en memoria:**
  * Crear nueva tarea con validación en tiempo real.
  * Mover tareas fluidamente hacia adelante y hacia atrás.
  * Eliminar tareas de forma individual.
* **Separación Estricta de Responsabilidades:**
  * Interfaz de usuario desacoplada de la lógica de negocio.
  * Cero lógica en el code-behind (`MainWindow.xaml.cs`).

---

## 🛠️ Tecnologías y Arquitectura

* **Lenguaje & Framework:** C# / .NET 8.0 (Windows Presentation Foundation - WPF)
* **Patrón de Diseño:** MVVM (Model-View-ViewModel)
* **Librería MVVM:** `CommunityToolkit.Mvvm` (Microsoft)
  * `ObservableObject` para la reactividad del ViewModel.
  * `[ObservableProperty]` para la generación de propiedades observables con Two-Way Data Binding.
  * `[RelayCommand]` para la ejecución de comandos desacoplados.
  * `ObservableCollection<T>` para la actualización en tiempo real de las columnas Kanban.
* **Principios SOLID aplicados:**
  * **S (Single Responsibility):** Cada clase tiene una única responsabilidad bien definida (Entidad de datos, Servicio de memoria, ViewModel, Vista XAML).
  * **D (Dependency Inversion):** El `MainViewModel` depende de la abstracción `ITareaService`, inyectada a través de su constructor.

---

## 📁 Estructura del Proyecto

```text
MiniKanban.WPF/
├── Models/
│   └── Tarea.cs              # Entidad POCO y Enums (EstadoTarea, PrioridadTarea)
├── Services/
│   ├── ITareaService.cs      # Contrato de operaciones CRUD
│   └── TareaService.cs       # Implementación del servicio en memoria
├── ViewModels/
│   └── MainViewModel.cs      # Lógica de presentación, colecciones y RelayCommands
├── Views/
│   ├── MainWindow.xaml       # Diseño visual de la ventana y Data Templates
│   └── MainWindow.xaml.cs    # Inicialización y asignación del DataContext
└── App.xaml                  # Punto de entrada de la aplicación
```

---

## 💻 Requisitos y Ejecución

1. Tener instalado **Visual Studio 2022** con la carga de trabajo de **Desarrollo de escritorio de .NET**.
2. Clonar el repositorio:
   ```bash
   git clone https://github.com/anthonybuitrago/MiniKanban.WPF.git
   ```
3. Abrir la solución `MiniKanban.WPF.sln`.
4. Compilar y ejecutar (`F5`).

---

## 👨‍💻 Autor
Desarrollado por **Anthony Buitrago** como parte de la práctica guiada de Arquitectura de Software y Patrones de Diseño.
