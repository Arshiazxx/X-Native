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

### Changed

- The README now states plainly what works today — cataloguing games from
  disk, standalone PE analysis, and the launcher UI — and that no game
  runs through X-Native yet. Its roadmap now marks the foundation items
  that the code backs as done.

### Notes

- No PC game runs through X-Native yet. The launch pipeline validates
  requests and resolves compatibility profiles, but there is no backend
  behind it: every otherwise-valid launch still ends in a failed
  `LaunchResult` with reason `BackendNotImplemented`.
