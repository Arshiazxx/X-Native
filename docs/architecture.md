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
| `IFileSystemService` | `DirectoryExists`; `FileExists`; `GetFiles`; `GetDirectories`; `OpenRead`; `ReadAllTextAsync` | `SystemFileSystemService`, in `XNative.Platform` |
| `IStorageService` | `ReadTextAsync`; `WriteTextAsync`; `ExistsAsync`; `DeleteAsync` | `FileStorageService`, in `XNative.Platform` |
| `IProcessService` | `CanStartProcesses`; `StartAsync`; `GetRunningProcess`; `StopAsync` | `SystemProcessService`, in `XNative.Platform`; `UnavailableProcessService`, in `XNative.XboxHost` (honest sandbox implementation: `CanStartProcesses` is `false`) |
| `IGameCatalog` | `GetGamesAsync`; `FindByIdAsync`; `FindByExecutablePathAsync`; `RefreshAsync` | `LocalGameCatalog`, in `XNative.Platform` |
| `IGameRunner` | `LaunchAsync(game)`; `LaunchAsync(game, LaunchOptions)` | `CompatibilityGameRunner`, in `XNative.Wine` — runs the shared launch pipeline (validation, profile resolution, structured failure); it does not execute games (see below) |
| `IDeviceInfoService` | `GetDeviceInfo()` | `UwpDeviceInfoService`, in `XNative.XboxHost`; `DesktopDeviceInfoService`, in `XNative.App` |
| `IGameProfileStore` | `GetProfiles()`; `FindForGame(game)` | `InMemoryGameProfileStore` and `JsonGameProfileStore`, in `XNative.Wine` |
| `ICompatibilityLayer` | `Id`; `DisplayName`; `CanRun(game)`; `RunAsync(game, options)` | `WineCompatibilityLayer`, in `XNative.Wine` — predicts from the profile store; `RunAsync` delegates to the same launch pipeline as the runner |
| `IInputService` | `GetGamepadState(playerIndex)`; `GetKeyboardState()`; `ConnectedGamepadCount` | `ManualInputService` (thread-safe, fed by the host) and `NullInputService`, in `XNative.Platform` |
| `ISettingsService` | `GetAsync`; `SaveAsync`; `ResetAsync` | `JsonSettingsService`, in `XNative.Platform` |
| `IGraphicsBackend` | `Id`; `DisplayName`; `SupportedApis`; `IsInitialized`; `Initialize()`; `Shutdown()` | `NullGraphicsBackend`, in `XNative.Graphics` — a lifecycle-complete backend that renders nothing; no real rendering backend exists yet |
| `IPluginHost` | `Log`; `PluginDirectory` | `PluginHost`, in `XNative.Plugins` |
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
- `XNative.Core.Input` — `GamepadState` and `KeyboardState` (immutable,
  validated snapshots), the `GamepadButtons` flags and `KeyboardKey` enum,
  and `InputConstants` (`MaxPlayers` = 4 player slots). `GamepadState`
  validates at construction, so an invalid snapshot cannot exist: axis
  and trigger values must be in range and not NaN, `buttons` must not
  set any bit outside the defined `GamepadButtons` flags (an undefined
  bit throws `ArgumentOutOfRangeException`), and the timestamp must be
  UTC — any other `DateTimeKind` throws `ArgumentException`, so snapshot
  timestamps are always comparable.
- `XNative.Core.Settings` — `AppSettings`, the persisted settings model
  (games root, minimum log level, scan depth, executable patterns) with a
  `Validate()` that rejects out-of-range values.

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
storage root), and `SystemProcessService` (which prunes processes that have
exited so their handles do not accumulate), together with
`LocalGameCatalog` (see [game-manifest.md](game-manifest.md) for what it
reads). A second registration, `AddXNativePlatformCore()`, registers the
same services *except* `IProcessService`, for hosts whose sandbox forbids
starting processes; those hosts supply their own honest implementation
(see the Xbox host below). The catalog's default root is the user's
documents folder, falling back to `LocalApplicationData/XNative/Games`
when the platform reports no documents folder (an empty root would
silently catalogue nothing). Disposing the catalog while a refresh is in
flight is safe: disposal is idempotent and the losing refresh returns
quietly instead of throwing. Manifest reads are bounded: the catalog reads
at most 1 MiB of a `game.json`, and an oversized manifest is treated as
unparseable — skipped, with the folder falling back to a bare executable
scan — so a scan never buffers an unbounded manifest into memory.
`FileLogService` rotates its file: when the active log reaches
`FileLogServiceOptions.MaximumFileSizeBytes` (5 MiB by default; a value of
zero or less disables rotation) it is renamed to a single backup —
`name.1.log` for `name.log`, replacing any previous backup — and the entry
being written starts a fresh file, keeping on-disk log data bounded at
roughly twice the threshold. Rotation is best-effort: a failed roll never
loses the entry being logged and never throws.

It also contains the PE inspector in `XNative.Platform.Executables`:
`PeFileInspector` parses the headers of a Windows executable (from a byte
array or a stream) into a `PeInfo` — validity, `ProcessorArchitecture`,
whether the file is a DLL, whether it is managed (.NET), its subsystem
(`PeSubsystem`), and the raw machine/subsystem values — without ever
throwing on malformed input; the stream overload reads at most a 1 MiB
header window and never closes the caller's stream.
`ExecutableClassifier` maps a `PeInfo` to an `ExecutableType` and
architecture. The catalog classifies every executable it records, on both
the manifest and the bare-scan path: it reads at most the first 64 KiB
through `IFileSystemService.OpenRead`, and a valid PE image populates the
game's `Architecture` and executable type. A file that is not a valid PE
image, or cannot be read at all, is still catalogued with the historical
defaults (`ExecutableType.WindowsExecutable`,
`ProcessorArchitecture.Unknown`) — classification never fails a scan.

Input lives in `XNative.Platform.Input`, registered separately by
`AddXNativeInput()` so heads opt in explicitly. `ManualInputService` is
the functional `IInputService`: a thread-safe store of the latest
gamepad/keyboard snapshots that a host feeds (on Xbox, from
`Windows.Gaming.Input`; see [xbox-host.md](xbox-host.md)). It starts with
every slot disconnected, exactly like `NullInputService`, which remains
available for heads that want "no input, ever" stated explicitly.
`DeadZone` and `GamepadStateMerger` are pure helpers over the core
snapshots.

Persisted settings live in `XNative.Platform.Settings`, registered by
`AddXNativeSettings()`. `JsonSettingsService` stores `AppSettings` as
versioned JSON (storage key `settings.json`, format version 1) over
`IStorageService`. Loading never throws on bad data: missing, unreadable,
or invalid stored values — and any stored format version other than the
one this build writes, including a newer one — yield the defaults, and
corrupt data is left untouched for diagnostics. The stored minimum log
level is parsed strictly: only an exact defined `LogLevel` member name
(case-insensitive) is accepted, so a numeric string or a comma-joined
composite such as `"Debug, Information"` falls back to the default for
that field only instead of being combined into a different level.
`ExecutablePatterns` is bounded by validation: between one and
`AppSettings.MaxExecutablePatterns` (32) entries, none blank and none
containing a directory separator — patterns name files, not paths. A
bare `*` is accepted deliberately: it costs the same single enumeration
per directory as any other pattern, and PE inspection decides what is
actually a game. `SettingsOptionsApplier` copies a loaded `AppSettings`
onto the catalog and log options objects (only a non-null
`GamesRootPath` overrides the catalog root); it runs
`AppSettings.Validate()` first and applies nothing when validation
fails, so an invalid instance cannot flow into the services.

### XNative.Graphics

The graphics module's infrastructure exists; a real rendering backend does
not. `AddXNativeGraphics()` registers the module descriptor, a
`GraphicsBackendRegistry` (backends keyed by `Id`, compared
case-insensitively; duplicate or empty ids are rejected with
`ArgumentException`), a `GraphicsBackendSelector`, and `NullGraphicsBackend`
as the resolvable `IGraphicsBackend`. The selector picks, among the backends
whose `SupportedApis` cover every required API, the one adding the fewest
extra APIs (ties break by registration order), and returns `null` when none
qualifies. The extra-API count is taken over the value's unsigned
representation, so it terminates for every input — including a
`SupportedApis` value with bits set outside the defined `GraphicsApi`
flags, which a custom backend could only produce through an unchecked
cast. `NullGraphicsBackend` (id `null`) is lifecycle-complete but
supports `GraphicsApi.None` only and renders nothing, so it is selected
only when no API is required. No backend translates or renders anything
yet.

### XNative.Wine

The compatibility layer module. It executes nothing — no backend exists and
the sandbox forbids one — but everything around execution is real: a launch
pipeline, a compatibility database, and structured, honest failure.
`AddXNativeWine()` registers the module descriptor, an `IGameProfileStore`
(an empty `InMemoryGameProfileStore` by default), `CompatibilityGameRunner`
as `IGameRunner`, and `WineCompatibilityLayer` as `ICompatibilityLayer`.

Both entry points delegate to one internal pipeline,
`CompatibilityLaunchPipeline`, so the runner and the layer can never
disagree about an outcome. For each launch the pipeline, in order:

1. Rejects a non-Windows executable type with
   `LaunchFailureReason.UnsupportedExecutableType`.
2. When a file system service is available, verifies the executable exists;
   a missing file fails with `LaunchFailureReason.ExecutableNotFound`.
3. Resolves the compatibility profile: an explicit
   `LaunchOptions.ProfileId` wins (an unknown id is logged as a warning and
   the launch continues unprofiled); otherwise the store's `FindForGame`
   matches by executable file name. Launch arguments resolve in the order
   per-launch options, the game's catalogued arguments, then the profile's
   recommended arguments.
4. Fails a profile rated `Broken` with `LaunchFailureReason.KnownBroken`,
   including the profile's notes in the message.
5. Otherwise fails with `LaunchFailureReason.BackendNotImplemented` — the
   honest end of every launch today. When a profile matched, the message
   names its rating.

Every step is logged when a log service is available. Constructing the
runner or the layer with no dependencies skips the file-system, profile,
and logging steps and reproduces the original stub results exactly.

`WineCompatibilityLayer` (`Id` `"wine"`, display name "Wine (research)")
adds `CanRun(game)`: a cheap prediction that returns the matching profile's
`CompatibilityRating`, or `Unknown` when the game has no profile (or no
store was supplied).

The profile stores live in `XNative.Wine.Profiles`:

- `InMemoryGameProfileStore` — a fixed list supplied at construction.
  Matching is by executable file name, case-insensitive, ignoring any
  directory portion of either path, so Windows-style paths match on any
  host.
- `JsonGameProfileStore` — loads a JSON array of
  `{ "id", "executableName", "rating", "notes"?, "recommendedArguments"? }`
  objects. The rating must be exactly one `CompatibilityRating` member
  name, matched case-insensitively: composite strings such as
  `"Broken, Starts"` and numeric strings such as `"3"` are not member
  names and are rejected like any other unknown rating (a lenient flags
  parse would silently read both as `Playable`). A malformed database is
  loud, never silently empty: invalid JSON, a missing required field, an
  unknown rating, an empty or whitespace `id`, or a duplicated `id`
  throws `InvalidDataException` at load time.

Both stores reject duplicate profile IDs (compared case-insensitively):
the in-memory store's constructor throws `ArgumentException`. A duplicate
would otherwise silently shadow the earlier profile, because profile
lookup resolves by ID.

### XNative.Plugins

Defines the plugin contracts: `IPlugin` and `IPluginCatalog`, which live in
this project rather than in the core, plus the core's `IPluginHost`.
`AddXNativePlugins()` registers the module descriptor, an empty
`InMemoryPluginCatalog` (a head that ships plugins registers its own
catalog or plugin instances instead), a `PluginHost` (the registered
`ILogService` plus the application base directory as the plugin
directory), and a `PluginManager`.

`InMemoryPluginCatalog` validates its contents at construction: a `null`
entry, a plugin whose `Id` is `null`, empty or whitespace, or two plugins
sharing an `Id` (compared case-insensitively) throw `ArgumentException`
naming the offending ID — a duplicate is never allowed to silently shadow
the plugin registered first.

The manager initialises each catalog plugin against the host with failure
isolation — one plugin throwing is logged, recorded as `Failed` in the
returned `PluginInitializationReport`, and never stops the others — and
attempts each plugin at most once per manager instance. The guarantees
are precise:

- Attempts are tracked per plugin *instance* as well as per ID. A second
  sighting of the same instance is reported `AlreadyInitialized`; a
  *different* instance claiming an ID the manager has already attempted
  is reported `Failed` (with an `InvalidOperationException`), because it
  can never run under that manager — the report never claims an outcome
  for a plugin that did not run. (The catalog check above rejects
  duplicates up front; this guard keeps the report honest for custom
  `IPluginCatalog` implementations that do not validate.)
- The isolation boundary covers the plugin's identity: if reading
  `IPlugin.Id` itself throws, or the ID is `null`, empty or whitespace,
  that plugin is recorded as `Failed` under its type name and the
  remaining plugins still initialise — one bad plugin cannot abort the
  run or suppress the report.
- Failure logging is best-effort: if the host's `ILogService` throws
  while a failure is being logged, the exception is swallowed. The report
  entry already carries the original failure, so a broken log sink can
  never abort initialisation or change its outcome.

Plugins are in-process only, by design: the UWP sandbox blocks loading
arbitrary assemblies at runtime, so there is no folder scanning and no
dynamic assembly loading. A plugin is a compiled-in `IPlugin`
implementation that a head places in the catalog.

### XNative.UI

View framework agnostic MVVM primitives: `ObservableObject`, `ViewModelBase`,
`RelayCommand`, `AsyncRelayCommand` and `INavigationService`, plus the view
models under `ViewModels`. It contains no views and no XAML, so the same view
models can be reused by a UWP head, by a desktop head or by tests.
`AddXNativeUI()` registers the module descriptor and
`GameLibraryViewModel` as singletons and `StartupViewModel` as a
transient. The asymmetry is deliberate: `GameLibraryViewModel` owns the
scanned library, and a head whose pages are not cached resolves it again
on every navigation — as a transient, each navigation would get a fresh,
empty view model and force a full rescan; as a singleton it rebinds the
instance that already holds the scan, the search text and the selection.
The singleton is per provider, so a host that disposes its provider and
builds a new one (a failed start, a reset) still starts from an empty
library. `StartupViewModel` carries no scan state and its `Load` is
cheap, so it stays transient and simply re-reads the current state on
each navigation.

`AsyncRelayCommand` wraps an asynchronous delegate: it reports
`CanExecute == false` while running, and an exception thrown by the delegate
never escapes onto the UI thread — it is captured in `LastError` and surfaced
through the `ErrorRaised` event.

`StartupViewModel` compares the module descriptors resolved from the container
against the expected module order (`xnative.core`, `xnative.platform`,
`xnative.graphics`, `xnative.plugins`, `xnative.wine`, `xnative.ui`) and
reports each shared module as loaded or missing. Any modules a head
registers beyond that shared set (for example `xnative.xboxhost`) are
appended afterwards in registration order and always reported as loaded —
a module is only listed because the container resolved it — so a
head-specific module never shows as missing in a head that does not
register it. The device description is reported alongside.

`GameLibraryViewModel` is the launcher surface. It owns the full scan result
(`Games`, ordered by name) and the projection a view binds to
(`VisibleGames`): `SearchText` filters by case-insensitive substring match
on the game name, and `SortMode` orders the result (`NameAscending` or
`NameDescending`). A rescan preserves `SelectedGame` when a game with the
same stable `Id` is still present. When constructed with an `IGameRunner`,
`LaunchSelectedCommand` launches the selected game — passing the game's
catalogued launch arguments through `LaunchOptions` — and `LaunchStatus`
carries the runner's result message for success and failure alike; without a
runner, launching is disabled and the command stays inert. `GameViewModel`
wraps one `GameInfo` with null-safe metadata properties (`Description`,
`Publisher`, `Version`), an `ArchitectureText` rendering, and the runner
label derived from the executable type.

### XNative.App

The development composition root and a runnable console head: it targets
`net8.0` with `OutputType` `Exe` (verified in `XNative.App.csproj`).
`CompositionRoot.AddXNative` registers every module — including the
settings and input registrations — plus `DesktopDeviceInfoService`;
`Build(gamesRootPath)` additionally points the game catalog at a
caller-supplied folder.

On top of the composition root sits a small diagnostic CLI (`Cli` /
`CommandLine`), which is what `Program.Main` runs:

| Command | What it does |
| --- | --- |
| `info [path]` | Prints the resolved modules, the device description, the connected-gamepad count, and the games in the games root. The default when no command is given; a single bare path argument is still accepted as the legacy form of `info <path>`. |
| `scan [path]` | Lists what a catalog scan of the games root finds, with architecture, executable type, and path. |
| `inspect <exe>` | Prints one executable's PE header information (validity, architecture, DLL, managed, subsystem, section count). |
| `launch <id-or-path>` | Resolves a game by catalog id or executable path (an existing file outside the catalog becomes an ad-hoc entry, classified on the spot) and sends it through `IGameRunner`, printing the structured result. |
| `settings show` | Prints the effective settings read back from the composed options, including which source supplied the games root, plus a `Source:` line for the stored settings themselves — loaded, absent, or rejected with the rejection category — when the settings service reports a load outcome. |
| `settings reset` | Discards the persisted settings and restores the defaults (persisted, so the reset survives a restart). |
| `help` | Prints the usage text. |

Exit codes are `0` for success, `1` for failure (missing file, invalid
PE, failed launch, a nonexistent games root, unexpected error), and
`2` for a usage error. The games-root failure applies only to a path
supplied explicitly on the command line: `scan` and `info` (including
the legacy single-path form of `info`) print
`Error: Games root not found: <path>` and exit `1` when the given path
does not exist, so a typo cannot masquerade as an empty library. With
no path argument — the platform default root or the settings file's
root — a missing root keeps the catalog's empty-library behaviour and
exits `0`.

Every command that builds the composition loads the persisted settings
first. Because options are baked into the platform singletons at first
resolution, loading happens in a throwaway pre-composition
(`SettingsBootstrap`): it resolves only `ISettingsService`, loads, and is
disposed; the real composition is then built from the loaded settings via
`Build(AppSettings, string)`, which applies them onto the catalog and log
options. The games-root precedence, highest first, is: an explicit
command-line path, then the persisted `AppSettings.GamesRootPath`, then
the platform module default. A settings load that itself fails never
stops the application: the defaults are substituted and a one-line
warning is printed to standard error. The load result also carries the
stored settings' outcome (`StoredOutcome`) when the settings service
reports one through `ISettingsLoadOutcomeProvider`: whether the stored
settings were loaded, were absent, or were rejected, with the rejection
category (corrupt data, unsupported version, invalid values, read
failure). It is `null` when the service does not report an outcome, or
when the load fell back before an outcome could be known — the
distinction matters because the substituted defaults are identical in
the absent and rejected cases. Only `settings show` renders it (the
`Source:` line); the other commands ignore it. This makes the whole
wiring — settings included — verifiable from the command line, outside
the Xbox environment.

### XNative.XboxHost

The UWP application head for Xbox Developer Mode. It owns the Windows Runtime
implementations and its own composition root, `Bootstrapper`, which calls the
same `AddXNativeX()` extensions as `XNative.App` — with one deliberate
difference: it composes `AddXNativePlatformCore()` (no desktop process
service) and registers its own `UnavailableProcessService`, because the
sandbox forbids starting processes. It does not reference `XNative.App`,
since that is an executable and depending on it would point a dependency
outward. The host also owns the `GamepadInputFeed`, which polls
`Windows.Gaming.Input` into the shared `ManualInputService`. See
[xbox-host.md](xbox-host.md).

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
Two registrations live inside the platform project but are opt-in rather
than part of `AddXNativePlatform()`: `AddXNativeInput()` and
`AddXNativeSettings()`, called explicitly by the heads that want them (the
console head calls both; the Xbox host calls `AddXNativeInput()`).

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
  DLL/managed detection, with tests built on synthetic PE files — and
  catalog integration: every catalogued executable is classified from its
  first 64 KiB, so a valid PE image's architecture is recorded on the
  game.
- The launch pipeline: executable-type and existence validation, profile
  resolution, and structured failure reasons — shared by the game runner
  and the Wine compatibility layer.
- Compatibility profile stores: in-memory and JSON-backed
  implementations of `IGameProfileStore`, matched to games by executable
  file name and consulted on every launch; both reject duplicate profile
  IDs loudly instead of shadowing.
- Input: gamepad/keyboard snapshot contracts in the core, a thread-safe
  manually-fed input service (plus a null implementation) in the
  platform, and an Xbox host feed polling `Windows.Gaming.Input`.
- Persisted settings: `AppSettings` with validation, a JSON-backed
  settings service that falls back to defaults on corrupt data, an
  options applier, and console-head wiring (`settings show` / `settings
  reset`, CLI > settings > platform-default precedence).
- Graphics and plugin infrastructure: a backend registry and
  capability-based selector with a null backend, and a plugin host,
  in-memory catalog, and failure-isolating plugin manager — all
  in-process; no rendering backend and no disk-loaded plugins.
- The diagnostic console CLI: `info`, `scan`, `inspect`, `launch`, and
  `settings` commands with 0/1/2 exit codes over the same composition
  root the Xbox head uses.
- The launcher view models: scanning, search/filter, name sorting,
  selection preserved across rescans, and launch through `IGameRunner`
  with the outcome surfaced as status text.
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
- Graphics translation: the registry, selector, and a null backend exist,
  but no backend implements `IGraphicsBackend` to actually render or
  translate anything.
- A curated compatibility database: the profile stores exist and profiles
  are resolved on every launch, but the default registration ships an
  empty store; the only profile data in the repo is the three-profile
  sample database in `examples/`.
- Plugin discovery and loading from disk: the catalog and host are
  implemented, but plugins are compiled-in implementations a head
  registers — the UWP sandbox forbids loading arbitrary assemblies at
  runtime, so no folder is ever scanned for plugin files.
- Settings wiring in the Xbox host: the console head loads and applies
  the persisted settings; the Xbox bootstrapper does not register
  `ISettingsService` yet.
