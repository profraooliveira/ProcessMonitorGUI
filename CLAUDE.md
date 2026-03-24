# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build                # Build
dotnet run                  # Run the Avalonia GUI app
dotnet clean && dotnet build  # Full rebuild
```

No tests exist in this project. No linter configured.

## Tech Stack

- .NET 8.0, C# with `LangVersion latest`
- Avalonia UI 11.2.x (cross-platform desktop GUI)
- CommunityToolkit.Mvvm 8.3.x (source generators: `[ObservableProperty]`, `[RelayCommand]`)

## Architecture

MVVM pattern with all GUI code under `MonitorGUI/`:

- **Models/** — `ProcessoInfo`, `ThreadInfo`, `MemoryBlockInfo` (plain data + observable props)
- **ViewModels/** — `MainWindowViewModel` (orchestrates monitoring loop, filtering, selection); `ViewModelBase` extends `ObservableObject`
- **Views/** — `MainWindow.axaml` (master-detail layout with DataGrids + memory map)
- **Services/** — `ProcessMonitorService` (enumerates system processes, classifies as CPU-BOUND/I/O-BOUND)
- **ViewLocator.cs** — Convention-based View↔ViewModel resolution (namespace swap)
- **App.axaml/.cs** — Application entry with FluentTheme + DataGrid theme

Entry point: `MonitorGUI/Program.cs`. The root `Program.cs` is a legacy console version excluded from build via `<Compile Remove>`.

## Key Patterns

- UI thread marshaling via `Dispatcher.UIThread.Post()` for background→UI updates
- `CancellationTokenSource` for start/stop monitoring lifecycle
- Partial method `OnProcessoSelecionadoChanged()` (CommunityToolkit source-gen) triggers thread list + memory map refresh
- Unix/macOS: defensive try/catch around `WaitReason` (kernel-restricted), returns "POSIX State" fallback

## Language

All UI text, variable names, comments, and model properties are in **Brazilian Portuguese**.
