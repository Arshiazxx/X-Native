# Architecture

## Principles

X-Native follows clean architecture. Dependencies point inward, towards
`XNative.Core`. The core defines what the platform can do, the modules decide
how it is done, and the composition root decides which implementation is used.

1. `XNative.Core` is platform independent and has no dependency of any kind.
2. Interfaces are declared in the core, implementations live in the modules.
3. Modules never reference each other, only the core.
4. The composition root is the only place aware of every module.

## Dependency graph

```
                +---------------+
                | XNative.App   |   composition root
                +-------+-------+
                        |
   +----------+---------+---------+-----------+
   |          |         |         |           |
+--v---+  +---v----+ +--v-----+ +-v-------+ +-v--------+
|  UI  |  |Platform| |Graphics| | Wine    | | Plugins  |
+--+---+  +---+----+ +--+-----+ +-+-------+ +-+--------+
   |          |         |         |           |
   +----------+----+----+---------+-----------+
                   |
              +----v-----+
              |   Core   |
              +----------+
```

No arrow ever leaves the core.

## Projects

### XNative.Core

Contains the domain model and the abstractions of the platform. It targets
`netstandard2.0` and references nothing, not even the dependency injection
abstractions, so that it can be consumed by any head and remains trivially
testable. A unit test enforces this rule by inspecting the referenced
assemblies of the compiled core.

The abstractions are real contracts with members, not placeholders. Where an
implementation exists it is listed; where only the contract exists, the
table says so — a contract is not an implementation.

| Interface | Members (summary) | Implemented by |
| --- | --- | --- |
| `ILogService` | `MinimumLevel`; `Log(level, message, exception?)`; `IsEnabled(level)` | `FileLogService` (default) and `NullLogService`, in `XNative.Platform` |
| `IFileSystemService` | `DirectoryExists`; `FileExists`; `GetFiles`; `GetDirectories`; `ReadAllTextAsync` | `SystemFileSystemService`, in `XNative.Platform` |
| `IStorageService` | `ReadTextAsync`; `WriteTextAsync`; `ExistsAsync`; `DeleteAsync` | `FileStorageService`, in `XNative.Platform` |
| `IProcessService` | `CanStartProcesses`; `StartAsync`; `GetRunningProcess`; `StopAsync` | `SystemProcessService`, in `XNative.Platform` |
| `IGameCatalog` | `GetGamesAsync`; `FindByIdAsync`; `FindByExecutablePathAsync`; `RefreshAsync` | `LocalGameCatalog`, in `XNative.Platform` |
| `IGameRunner` | `LaunchAsync(game)`; `LaunchAsync(game, LaunchOptions)` | `CompatibilityGameRunner`, in `XNative.Wine` — a stub that reports failure; it does not execute games (see below) |
| `IDeviceInfoService` | `GetDeviceInfo()` | `UwpDeviceInfoService`, in `XNative.XboxHost`; `DesktopDeviceInfoService`, in `XNative.App` |
| `IGameProfileStore` | `GetProfiles()`; `FindForGame(game)` | Contract only — intended for `XNative.Wine`; no implementation yet |
| `ICompatibilityLayer` | `Id`; `DisplayName`; `CanRun(game)`; `RunAsync(game, options)` | Contract only — intended for `XNative.Wine`; no implementation yet |
| `IGraphicsBackend` | `Id`; `DisplayName`; `SupportedApis`; `IsInitialized`; `Initialize()`; `Shutdown()` | Contract only — intended for `XNative.Graphics`; no implementation yet |
| `IPluginHost` | `Log`; `PluginDirectory` | Contract only — intended for `XNative.Plugins`; no implementation yet |
| `IModuleDescriptor` | `Id`; `DisplayName` | One descriptor class per module (for example `CoreModuleDescriptor`, `PlatformModuleDescriptor`) |

`IDeviceInfoService` returns a `DeviceInfo`, an immutable model declared in
`XNative.Core.Devices`. It is implemented by the application heads rather
than by `XNative.Platform` because the Xbox implementation reads it through
the Windows Runtime, which a .NET Standard library cannot reference. The
desktop implementation (`DesktopDeviceInfoService`) uses only the standard
library (`Environment`, `RuntimeInformation`) and reports
`DeviceInfo.UnknownValue` for values it has no honest source for, such as
the manufacturer.

The core's model types are grouped by namespace:

- `XNative.Core.Devices` — `DeviceInfo`, and `ProcessorArchitecture`
  (`Unknown`, `X86`, `X64`, `Arm`, `Arm64`), the shared vocabulary for the
  architecture of a device or of an executable.
- `XNative.Core.Games` — `GameInfo`, `GameMetadata`, `ExecutableType`,
  `LaunchOptions` (per-launch arguments, working directory, environment
  variables, profile id), `LaunchResult`, `LaunchFailureReason`
  (machine-readable launch failure reasons, from `BackendNotImplemented` to
  `BlockedBySandbox`), `GameProfile`, and `CompatibilityRating` (`Unknown`,
  `Broken`, `Starts`, `Playable`, `Perfect`).
- `XNative.Core.Logging` — `LogLevel`, and `LogServiceExtensions`
  (`Trace`/`Debug`/`Information`/`Warning`/`Error` convenience methods over
  `ILogService`).
- `XNative.Core.Processes` — `ProcessStartRequest` and `RunningProcess`,
  the models used by `IProcessService`.
- `XNative.Core.Graphics` — `GraphicsApi`, the flags enum of source APIs a
  graphics backend can translate.

Services are configured through the options pattern: modules register
defaults with `Configure<TOptions>(...)` and heads may override them. The
options classes in use today are `LocalGameCatalogOptions`,
`FileLogServiceOptions`, and `FileStorageServiceOptions`.

### XNative.Platform

Holds every implementation that touches the operating system. Isolating the
platform here is what keeps the Xbox and UWP sandbox rules out of the rest of
the codebase.

It implements the platform services registered by `AddXNativePlatform()`:
`SystemFileSystemService`, `FileLogService` (plus the discarding
`NullLogService`), `FileStorageService` (atomic writes, keys confined to its
storage root), and `SystemProcessService`, together with `LocalGameCatalog`
(see [game-manifest.md](game-manifest.md) for what it reads).

It also contains the PE inspector in `XNative.Platform.Executables`:
`PeFileInspector` parses the headers of a Windows executable (from a byte
array or a stream) into a `PeInfo` — validity, `ProcessorArchitecture`,
whether the file is a DLL, whether it is managed (.NET), its subsystem
(`PeSubsystem`), and the raw machine/subsystem values — without ever
throwing on malformed input. `ExecutableClassifier` maps a `PeInfo` to an
`ExecutableType` and architecture. The inspector is a standalone component:
the game catalog does not call it yet, and catalogued games are recorded as
`ExecutableType.WindowsExecutable` with `ProcessorArchitecture.Unknown`.

### XNative.Graphics

Boundary for a future rendering backend. It currently contains only its
module registration and descriptor. The `IGraphicsBackend` contract and the
`GraphicsApi` flags exist in the core, but no backend implements them and
no graphics translation takes place.

### XNative.Wine

Boundary for a future compatibility layer. `AddXNativeWine()` registers its
module descriptor and `CompatibilityGameRunner` as `IGameRunner`. The runner
is a stub: it recognises Windows executables and returns a failed
`LaunchResult` ("Compatibility backend not implemented"); every other
executable type fails as unsupported. It executes nothing. The
`ICompatibilityLayer` and `IGameProfileStore` contracts it is intended to
host have no implementation yet, and no profile is resolved during a launch.

### XNative.Plugins

Defines the plugin contracts: `IPlugin` and `IPluginCatalog`, which live in
this project rather than in the core, plus the core's `IPluginHost`. The
module registers only its descriptor. There is no catalog implementation,
no plugin host implementation, and no loading of plugins from disk.

### XNative.UI

View framework agnostic MVVM primitives: `ObservableObject`, `ViewModelBase`,
`RelayCommand` and `INavigationService`, plus the view models under
`ViewModels`. It contains no views and no XAML, so the same view models can be
reused by a UWP head, by a desktop head or by tests.

`StartupViewModel` compares the module descriptors resolved from the container
against the list of expected modules and reports each one as loaded or missing.
`GameLibraryViewModel` scans through `IGameCatalog` and exposes the results
for the library screen.

### XNative.App

The development composition root and a runnable console head: it targets
`net8.0` with `OutputType` `Exe` (verified in `XNative.App.csproj`).
`CompositionRoot.AddXNative` registers every module plus
`DesktopDeviceInfoService`; `Build(gamesRootPath)` additionally points the
game catalog at a caller-supplied folder. Running it —

```bash
dotnet run --project src/XNative.App -- <folder-with-games>
```

— prints the resolved modules and the device description, scans the folder
with the catalog, and prints the games found. It exits `0` on success; any
failure is reported as `Error: <message>` on standard error with exit code
`1`. This makes the whole wiring verifiable from the command line, outside
the Xbox environment.

### XNative.XboxHost

The UWP application head for Xbox Developer Mode. It owns the Windows Runtime
implementations and its own composition root, `Bootstrapper`, which calls the
same `AddXNativeX()` extensions as `XNative.App`. It does not reference
`XNative.App`, since that is an executable and depending on it would point a
dependency outward. See [xbox-host.md](xbox-host.md).

## Module registration

Each module exposes one extension method on `IServiceCollection`:

```csharp
services.AddSingleton<IModuleDescriptor, CoreModuleDescriptor>();
services
    .AddXNativePlatform()
    .AddXNativeGraphics()
    .AddXNativeWine()
    .AddXNativePlugins()
    .AddXNativeUI();
```

The core cannot register itself, because it has no dependency on a container,
so each composition root registers `CoreModuleDescriptor` explicitly. The Xbox
host adds `AddXboxHost()` on top for its Windows Runtime implementations.

Adding a module means adding a project, an extension method and one line in
each composition root. Nothing else in the codebase changes.

## Target frameworks

Libraries target `netstandard2.0`, which is the highest surface a UWP
application can reference. `XNative.App` and the test projects target `net8.0`
so that the modern SDK tooling can build and run them. `XNative.XboxHost`
targets UAP `10.0.19041.0` and references the same `netstandard2.0` libraries
unchanged.

Because the .NET SDK cannot evaluate a legacy UWP project, `XNative.XboxHost`
is mapped to `Debug|x64` under the `Any CPU` solution configuration but is not
built there. `dotnet build` and `dotnet test` therefore keep working on the
whole solution, while MSBuild builds the host under `x64` or `x86`.

## Implemented

- The core contracts and models listed above (abstractions, games, logging,
  processes, graphics, and devices namespaces).
- The platform services: file system, file/ null logging, file storage, and
  process services, all registered for dependency injection.
- The local game catalog: recursive scanning, `game.json` manifests
  (including the v2 `arguments` and `icon` fields), stable game IDs, and a
  path-traversal guard on manifest paths.
- PE inspection: header parsing, architecture/subsystem classification, and
  DLL/managed detection, with tests built on synthetic PE files.
- The module system: one descriptor and one registration extension per
  module, resolved and reported at startup by both heads.
- Startup reporting: the console head prints modules, device info, and scan
  results; the Xbox host shows module health and startup failures on its
  startup screen.

## Not implemented

- Actually running games. No compatibility layer executes a game today, and
  the UWP sandbox forbids the process creation, foreign-code loading, and JIT
  compilation a classic layer needs — see
  [compatibility.md](compatibility.md).
- Wine itself, and any `Box86`/`Box64`-style emulation.
- Graphics translation: no `IGraphicsBackend` implementation exists.
- A compatibility profile store: no `IGameProfileStore` implementation
  exists, so profiles are never resolved or applied at runtime.
- Plugin loading from disk: no `IPluginCatalog` or `IPluginHost`
  implementation exists.
- Catalog integration of the PE inspector: catalogued executables are not
  yet inspected, so their recorded architecture stays `Unknown`.
