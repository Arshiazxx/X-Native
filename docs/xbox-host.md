# Xbox host

`src/XNative.XboxHost` is the UWP application head of X-Native. It is the only
project allowed to call the Windows Runtime.

## Configuration

| Setting | Value |
| --- | --- |
| Project type | Legacy UWP (`AppContainerExe`) |
| Target platform version | `10.0.19041.0` |
| Target platform minimum version | `10.0.17763.0` |
| Platforms | `x86`, `x64` |
| Device families | `Windows.Universal`, `Windows.Xbox` |

The Xbox operating system tracks the `19041` era rather than the newest desktop
SDK, so `10.0.19041.0` is targeted even though `10.0.26100.0` is installed.
Xbox deploys the `x64` configuration.

## NuGet packages

Only two, both unavoidable:

- `Microsoft.NETCore.UniversalWindowsPlatform`, mandatory for any UWP project.
- `Microsoft.Extensions.DependencyInjection`, the container already used by the
  rest of the solution.

## Dependency direction

The host references `Core`, `Platform`, `UI`, `Graphics`, `Wine` and `Plugins`.
It deliberately does **not** reference `XNative.App`, which is a `net8.0`
executable; depending on it would point a dependency outward. Instead the host
calls the same `AddXNativeX()` extension methods, so both heads share the
registration pattern without sharing a project.

```
XNative.XboxHost ──> UI, Platform, Graphics, Wine, Plugins ──> Core
```

`UwpDeviceInfoService` lives in the host, not in `XNative.Platform`, because
the module libraries target .NET Standard and cannot reference the Windows
Runtime. It implements `IDeviceInfoService`, which is declared in the core, so
the dependency still points inward.

## Services

`Bootstrapper.AddXNative` registers the core descriptor, the modules
(`AddXNativeGraphics()`, `AddXNativeWine()`, `AddXNativePlugins()`,
`AddXNativeUI()`), the input registration (`AddXNativeInput()`), and
finally `AddXboxHost()`, which supplies the host's own descriptor and
`UwpDeviceInfoService` as the `IDeviceInfoService`.

The platform module is composed differently from the console head, on
purpose. The host calls `AddXNativePlatformCore()` — every platform
service *except* `IProcessService` — and registers its own
`UnavailableProcessService` in its place: the desktop implementation
wraps `System.Diagnostics.Process`, which the UWP sandbox forbids, so
the host's implementation states the limitation instead
(`CanStartProcesses` is `false`, `StartAsync` throws
`PlatformNotSupportedException`, `GetRunningProcess` returns `null`, and
`StopAsync` reports `true` — nothing can ever have been started). The
rest of the service set — file system, logging, storage, and the local
game catalog — matches the console head, under UWP sandbox rules, which
is why broad file access behaves as described in
[compatibility.md](compatibility.md). One service the console head has
that the host does not register yet is `ISettingsService`: the persisted
settings are currently a console-head concern.

`AddXNative` also accepts an optional games-root path that overrides the
catalog's root. Games on Xbox live on removable or external storage
rather than in the default documents folder; the seam exists so the host
can pass that location once it knows it. `Bootstrapper.Start()` currently
passes nothing, so the platform default applies.

## Startup sequence

1. `App.OnLaunched` sets the view bounds mode and calls
   `Bootstrapper.Start`, caching the report in `App.Startup`. A
   *successful* start is cached for the process lifetime; a failed start
   is not — the next activation retries `Start` instead of reusing the
   failure. It then creates the root frame if needed. Navigation and
   `Window.Current.Activate()` happen only on a real launch: during
   prelaunch activation the operating system starts the process ahead of
   time so the real launch is fast, so `Start` still runs (the
   composition is ready when the user actually launches) but the page is
   not navigated to and the window is not surfaced.
2. `Bootstrapper.Start` registers every module, builds the container with
   `ValidateOnBuild` and `ValidateScopes`, then resolves every
   `IModuleDescriptor` and runs the device service's detection
   (`GetDeviceInfo()`) so that a wiring mistake fails at startup rather
   than later. Running detection here matters: it is lazy, and the WinRT
   calls behind it are the one genuinely fallible part of startup — left
   to itself, detection would first run on the first page, outside the
   guarded region. Calling `Start` on an already-started host returns the
   existing provider as a success. `Start` and `Reset` are serialised on
   a private lock, so the static startup state does not depend on
   activation staying single-threaded.
3. `Start` never throws: **any** exception from the build or the startup
   resolutions is caught and returned as `StartupReport.Failed`, so a
   broken registration reaches the startup error screen instead of crashing
   `App.OnLaunched`. If the container was built before the failure, it is
   disposed before the failure is returned — it is never handed out, so
   nothing else could dispose it, and a retry would otherwise build a
   second container beside the leaked one.
4. `MainPage.OnNavigatedTo` checks the report. If startup failed (or never
   ran), the page shows the failure and stops. Otherwise it resolves
   `StartupViewModel` and calls `Load` — guarded: a failure escaping
   `Load` is shown on the page instead of escaping navigation, where
   `OnNavigationFailed` would turn it into a process crash. (Detection
   already ran during startup, so this should be unreachable; it is
   belt-and-braces.) It then resolves `GameLibraryViewModel`, starts the
   gamepad input feed (see below), and updates the bindings. Navigating
   away stops the feed.
5. `Bootstrapper.Reset()` disposes the service provider, clears the
   started state so `Start` can run again, and clears the report cached
   in `App.Startup` — otherwise the app-level cache would keep handing
   out a success whose provider has been disposed. It is test and support
   tooling only — the running application never calls it, because a UWP
   host starts exactly once per process.

The startup screen lists the expected modules in a fixed order and marks each
one as loaded or missing, so a module that fails to register is visible on the
console screen.

## Input

`GamepadInputFeed` (in the host's `Input` folder) bridges the Xbox
operating system's gamepad API into the platform's polling
`IInputService` contract. It polls `Windows.Gaming.Input` on a
`DispatcherTimer` at a 16 ms interval (~60 Hz) and writes every reading
into the shared `ManualInputService` singleton as an immutable core
`GamepadState`; the `GamepadAdded` / `GamepadRemoved` events only trigger
an immediate extra poll, so the poll loop stays the single writer. A
pad's player slot is its position in `Gamepad.Gamepads`, capped at
`InputConstants.MaxPlayers` (4); slots with no pad are written
`GamepadState.Disconnected` on every poll, so a removed pad never leaves
a stale "connected" snapshot behind. The whole poll body — taking the
`Gamepad.Gamepads` snapshot included — runs inside an exception guard: a
poll executes unattended on a timer tick, where an escaping exception
would be an unhandled-exception process crash in UWP, so a fault costs
one missed poll (the next tick retries against a fresh snapshot) and the
slots keep their last written states.

Two mapping losses are deliberate and documented in the feed's own
remarks: the UWP `Paddle1`–`Paddle4` buttons have no counterpart in the
core `GamepadButtons` and are dropped, and the core `Guide` button is
never set — the system reserves the Xbox button and readings never
report it. The main page starts the feed when it is navigated to and
stops it (resetting every slot to disconnected) when navigated away
from.

Like the rest of the host, the feed is written against the UAP
10.0.19041 surface but has not been verified by a compiler — the legacy
UWP project cannot be built outside Windows (see the manifest caveat
under Capabilities).

## Launching from the main page

The games list is a selectable `ListView` bound to the game library view
model's `VisibleGames` — its filtered/sorted projection, not the raw
catalog result — with the selection held by the view model
(`SelectedGame`, bound two-way), so the view model's selection
preservation across rescans applies to this list too. The
**Launch Selected Game** button takes the selected game, looks its
`GameInfo` up in `IGameCatalog` by executable path, and calls
`IGameRunner.LaunchAsync` — passing the game's manifest launch arguments
through `LaunchOptions` when it has any, mirroring the launcher view
model's launch path instead of silently dropping them. The handler also
mirrors the view model's re-entry guard: a second tap while a launch is
in flight is ignored, so two launches cannot run concurrently against
the singleton runner. The outcome is shown next to the button;
failures and exceptions are also mirrored into the page's error element, so
the handler cannot crash the page. Today the registered runner runs the
launch pipeline — validation and profile resolution — but there is no
backend behind it, so a launch attempt ends in a structured failure
(`BackendNotImplemented`, or `KnownBroken` for a game whose profile rates it broken)
and no game is executed (see [compatibility.md](compatibility.md)).

## Capabilities

The package manifest (`Package.appxmanifest`) declares:

- `internetClient` — outbound network access.
- `removableStorage` — access to removable storage devices, where games may
  be placed.
- `broadFileSystemAccess` — a **restricted** capability
  (`rescap:Capability`). It grants file system access beyond the app
  sandbox, requires the user to grant consent at runtime (Settings >
  Privacy), and is subject to additional Store review. It is requested
  because X-Native is a Developer Mode launcher whose whole purpose is to
  discover games the user placed on internal or removable storage; that use
  is appropriate for Dev Mode sideloading, not for a general Store
  submission without further justification.

Acceptance of this capability set by the Xbox Dev Mode packaging path has
not yet been validated by a real MSBuild package build; that check happens
on the first Windows build of the host.

## Deploying to a console in Developer Mode

1. Put the console into Developer Mode and note its IP address.
2. Build the package:
   `msbuild src\XNative.XboxHost\XNative.XboxHost.csproj /p:Configuration=Debug /p:Platform=x64`
3. Open the Device Portal at `https://<console-ip>:11443`.
4. Under **Home > Add**, upload the `.msix` from
   `src\XNative.XboxHost\AppPackages\`, together with the dependency `.appx`
   files from the `Dependencies\x64` folder and the `.cer` file.
5. Start **X-Native** from the Dev Home.

From Visual Studio, select `Debug | x64`, choose **Remote Machine**, enter the
console address and deploy.

## Signing

`XNative.XboxHost_TemporaryKey.pfx` is a development certificate created for
local sideloading. It is ignored by git and must not be used for anything
beyond Developer Mode testing.
