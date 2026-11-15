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

`Bootstrapper.AddXNative` registers the core descriptor, every module
(`AddXNativePlatform()`, `AddXNativeGraphics()`, `AddXNativeWine()`,
`AddXNativePlugins()`, `AddXNativeUI()`), and finally `AddXboxHost()`, which
supplies the host's own descriptor and `UwpDeviceInfoService` as the
`IDeviceInfoService`. The platform registrations include the file system,
logging, storage, and process services and the local game catalog, so the
host resolves the same service set as the console head — under UWP sandbox
rules, which is why process creation and broad file access behave as
described in [compatibility.md](compatibility.md).

## Startup sequence

1. `App.OnLaunched` sets the view bounds mode and calls
   `Bootstrapper.Start` (once — the report is cached in `App.Startup`),
   creates the root frame if needed, and navigates to `MainPage`.
2. `Bootstrapper.Start` registers every module, builds the container with
   `ValidateOnBuild` and `ValidateScopes`, then resolves every
   `IModuleDescriptor` and the device service so that a wiring mistake
   fails at startup rather than later. Calling `Start` on an
   already-started host returns the existing provider as a success.
3. `Start` never throws: **any** exception from the build or the startup
   resolutions is caught and returned as `StartupReport.Failed`, so a
   broken registration reaches the startup error screen instead of crashing
   `App.OnLaunched`.
4. `MainPage.OnNavigatedTo` checks the report. If startup failed (or never
   ran), the page shows the failure and stops. Otherwise it resolves
   `StartupViewModel`, calls `Load`, resolves `GameLibraryViewModel`, and
   updates the bindings.
5. `Bootstrapper.Reset()` disposes the service provider and clears the
   started state so `Start` can run again. It is test and support tooling
   only — the running application never calls it, because a UWP host starts
   exactly once per process.

The startup screen lists the expected modules in a fixed order and marks each
one as loaded or missing, so a module that fails to register is visible on the
console screen.

## Launching from the main page

The games list is a selectable `ListView` bound to the game library. The
**Launch Selected Game** button takes the selected game, looks its
`GameInfo` up in `IGameCatalog` by executable path, and calls
`IGameRunner.LaunchAsync`. The outcome is shown next to the button;
failures and exceptions are also mirrored into the page's error element, so
the handler cannot crash the page. Today the registered runner is a stub, so
a launch attempt reports that the compatibility backend is not implemented —
no game is executed (see [compatibility.md](compatibility.md)).

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
