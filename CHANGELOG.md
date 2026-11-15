# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Real core contracts for the platform services — logging, storage,
  processes, the compatibility layer, the graphics backend, and the plugin
  host — replacing the member-less placeholder interfaces, together with
  launch options (`LaunchOptions`), structured launch results
  (`LaunchResult` / `LaunchFailureReason`), and compatibility game profiles
  (`GameProfile` / `CompatibilityRating`).
- Platform service implementations — `SystemFileSystemService`,
  `FileLogService` (plus the discarding `NullLogService`),
  `FileStorageService`, and `SystemProcessService`, all registered for
  dependency injection — so a composition root can now construct the full
  service set, including the game catalog.
- Stable game IDs in the local game catalog: IDs are an FNV-1a 64-bit hash
  of the executable's full path, so a catalogued game keeps its identity
  across rescans and restarts (moving a game changes its ID).
- Manifest v2 support in the catalog: `game.json` `arguments` are recorded
  as the game's launch arguments and `icon` as its icon path (only when
  the icon stays inside the manifest folder and exists), and a
  path-traversal guard now rejects manifests whose `executable` or
  `workingDirectory` escapes the manifest folder.
- A PE inspector (`PeFileInspector` / `ExecutableClassifier`) that reads
  the headers of Windows executables and reports their architecture,
  subsystem, and DLL/managed characteristics, with tests built on
  synthetic PE files.
- A real launch pipeline in `XNative.Wine`, shared by
  `CompatibilityGameRunner` and the new `WineCompatibilityLayer`
  (`ICompatibilityLayer`): executable-type and existence validation,
  compatibility profile resolution (an explicit `LaunchOptions.ProfileId`
  wins; launch arguments resolve options → game → profile), and
  structured failures — a profile rated `Broken` now fails the launch as
  `KnownBroken`, and every other valid launch ends at
  `BackendNotImplemented`. Every step is logged. The parameterless
  constructors preserve the original stub behaviour exactly.
- Compatibility profile stores (`IGameProfileStore` implementations):
  `InMemoryGameProfileStore` and `JsonGameProfileStore`, matched to games
  by executable file name. The JSON store fails loudly
  (`InvalidDataException`) on a malformed database instead of silently
  behaving as empty. `WineCompatibilityLayer.CanRun` predicts a game's
  rating from its profile (`Unknown` when the game has no profile). The default module
  registration ships an empty store; no curated profile database ships
  yet.
- Launcher features in the UI view models: `GameLibraryViewModel` gained
  a filtered/sorted `VisibleGames` projection (`SearchText` substring
  filter, `SortMode` name ordering), selection preserved across rescans
  by stable game ID, and launching through an optional `IGameRunner` —
  the selected game's catalogued arguments are passed via `LaunchOptions`
  and the outcome is surfaced as `LaunchStatus`. New `AsyncRelayCommand`
  captures delegate exceptions into `LastError` / `ErrorRaised` instead
  of letting them escape onto the UI thread, and `GameViewModel` exposes
  null-safe metadata plus architecture/runner display text.
  `StartupViewModel`'s expected-module list now includes `xnative.ui`.
- A runnable console head: `XNative.App` now targets `net8.0` and runs —
  it prints the resolved modules and device information
  (`DesktopDeviceInfoService`) and scans a folder with the game catalog as
  an end-to-end smoke test.
- Xbox host hardening: the bootstrapper reports any startup failure
  instead of throwing (plus a `Reset` for tests and support), the main
  page gained game selection and launch wiring, and the package manifest
  declares the `removableStorage` and `broadFileSystemAccess`
  capabilities.
- Continuous integration: GitHub Actions builds and tests the managed
  solution on Windows and Linux, with a documentation link check.
- Repository documentation: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
  `SECURITY.md`, an MIT `LICENSE` file, issue and pull request templates,
  and an `.editorconfig`.
- Documentation for the `game.json` manifest format, for building the
  project, and for the platform constraints that shape the compatibility
  work.
- PE classification during catalog scans: every catalogued executable is
  inspected through `IFileSystemService.OpenRead`, reading at most the
  first 64 KiB, and a valid PE image populates the game's
  `GameInfo.Architecture`. Files that are not valid PE images (or cannot
  be read) are still catalogued with the historical defaults, so a scan
  never fails because of one bad executable.
- Input contracts and services: gamepad and keyboard state models
  (`GamepadState` / `KeyboardState`, validated immutable snapshots) and
  `IInputService`, with `ManualInputService` (thread-safe, fed by a host)
  and `NullInputService` implementations, plus dead-zone and multi-source
  state-merging helpers. On Xbox, a host feed polls
  `Windows.Gaming.Input` at ~60 Hz into the shared manual service, so the
  same input contracts serve both heads.
- Graphics and plugin infrastructure behind the core contracts: a
  `GraphicsBackendRegistry`, a capability-based `GraphicsBackendSelector`,
  and a `NullGraphicsBackend`; and a plugin host with an in-memory
  catalogue and a `PluginManager` that initialises plugins with failure
  isolation (one failing plugin is reported, never fatal to the rest).
- A persisted settings service: `AppSettings` (games root, minimum log
  level, scan depth, executable patterns) with a `JsonSettingsService`
  over `IStorageService` — a corrupt settings file falls back to defaults
  instead of throwing — plus helpers that apply settings to the catalog
  and logging configuration.
- The console head now loads the persisted settings before composing:
  `settings show` prints the effective values (including which source won
  the games root) and `settings reset` restores the defaults. Precedence
  is command-line path, then the settings file, then the platform default.
- A diagnostic command line in the console head: `info` (the default —
  modules and device information), `scan` (catalogue a folder),
  `inspect <exe>` (full PE details for one executable), and
  `launch <id-or-path>` (a structured launch result with honest non-zero
  exit codes: 0 success, 1 launch failure, 2 usage error). A bare path
  argument still works as before.
- Xbox composition for the UWP sandbox: the platform module now offers a
  core registration without the desktop process service, and the Xbox
  host supplies its own `IProcessService` that honestly reports it cannot
  start processes (`CanStartProcesses` is `false`; `StartAsync` throws
  `PlatformNotSupportedException`). The bootstrapper accepts an optional
  games-root override, a failed startup is retried on the next launch
  instead of being cached forever, and launching from the main page now
  passes the game's manifest launch arguments.
- An `examples/` pack: three sample games (a v1 manifest game, a v2
  manifest game using every catalogued field with a real icon, and a bare
  executable with no manifest) and a three-profile compatibility database
  (Playable / Starts / Broken). Tests pin the examples against the real
  catalog and profile store so they cannot silently drift from the code.
- An integration test suite covering composition roots, catalog
  end-to-end scans, adversarial core models, and view-model behaviour.
  The solution now references all eight test projects, and CI also tests
  each project explicitly and fails if a test project exists that is
  covered by neither the solution nor the CI list.
- A `global.json` pinning the .NET 8 SDK band (8.0.100,
  `latestFeature` roll-forward) so builds do not drift with newer SDKs,
  and a `.gitattributes` fixing line endings per file type.

### Changed

- The README now states plainly what works today — cataloguing games from
  disk, standalone PE analysis, and the launcher UI — and that no game
  runs through X-Native yet. Its roadmap now marks the foundation items
  that the code backs as done.
- `LaunchOptions.Default` now returns a fresh instance on every access.
  It was previously a shared mutable singleton, so one caller changing
  the "default" options silently changed them for everyone.
- Core model validation is consistent: `DeviceInfo` and
  `RunningProcess` now reject empty or whitespace strings exactly like
  `GameInfo` and `GameProfile` already did, and `DeviceInfo.IsXbox`
  matches only the Xbox device family itself (`Windows.Xbox`, `Xbox`,
  or `Windows.Xbox.*`) instead of any family string containing "Xbox".
- Windows executables are labelled "wine (research)" in the launcher UI,
  matching the compatibility layer's own display name, instead of the
  plain "wine" label that overstated what exists.
- The view models' documentation now states their thread-affinity
  contract explicitly: collections are mutated and notifications raised
  on the caller's thread with no internal synchronisation, so a host
  must call them from its UI thread.
- The build and contributing guides were corrected against the current
  tree (SDK pin, the full eight-project test suite, and the console
  head's commands), and the GitHub templates and CI comments now match
  the test setup they describe.

### Fixed

- `SystemProcessService.StopAsync` no longer orphans a tracked process
  when its grace-period wait is cancelled: the entry is removed only
  after the process has exited or been killed, so the process stays
  stoppable. Exited processes are pruned on start, and a process that
  exited on its own is reported once (with its exit code) and then
  pruned instead of lingering forever.
- `LocalGameCatalog.Dispose` racing an in-flight refresh could throw
  `ObjectDisposedException` out of `RefreshAsync`; disposal is now
  idempotent and thread-safe, and a refresh that loses the race returns
  quietly without corrupting the published snapshot.
- The catalog's default games root was the user's documents folder
  unconditionally; on hosts where that folder does not resolve (for
  example a Linux shell without XDG user directories) it came back
  empty and the catalog silently scanned nothing. It now falls back to
  `LocalApplicationData/XNative/Games`, alongside the storage and log
  roots.
- `PeFileInspector.Inspect(Stream)` read the entire stream into memory
  (twice) with no bound; it now reads at most a 1 MiB header window —
  far more than any PE header needs — and never closes the caller's
  stream.
- The profile stores silently accepted duplicate profile IDs, so a later
  profile shadowed an earlier one. The JSON store now throws
  `InvalidDataException` naming the duplicated ID, and the in-memory
  store throws `ArgumentException`; both also reject empty or whitespace
  profile IDs.
- `FileLogService` accepted any log `FileName`, including path
  separators and `..` traversal; it now validates the name at
  construction with the same rules as storage keys. `FileStorageService`
  keys additionally reject reserved DOS device names (`CON`, `NUL`,
  `COM1`, …) and names ending in a dot or space, which alias devices or
  fail unpredictably on Windows.
- `RelayCommand.Execute` ignored `CanExecute` and ran the delegate even
  when the command was disabled; it is now a no-op when `CanExecute`
  returns `false`, matching `AsyncRelayCommand`.
- The launch pipeline silently ignored a supplied `WorkingDirectory` or
  `EnvironmentVariables`; it now logs a warning naming each option as
  not applied by the current compatibility backend (the launch result
  itself is unchanged).

### Notes

- No PC game runs through X-Native yet. The launch pipeline validates
  requests and resolves compatibility profiles, but there is no backend
  behind it: every otherwise-valid launch still ends in a failed
  `LaunchResult` with reason `BackendNotImplemented`.
