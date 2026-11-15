# Compatibility: what the platform allows, and what that means

This page is the honest version of X-Native's goal. Running arbitrary PC
games on an Xbox in Developer Mode is constrained — hard — by the platform
itself. This document states the constraints, what they mean for each part of
the roadmap, what X-Native does today, and which research directions are
credible. Nothing here is a promise that any particular game will ever run.

## The sandbox X-Native lives in

X-Native's Xbox head is a UWP application running in Developer Mode. UWP's
sandbox is designed to stop exactly the things a traditional compatibility
layer does:

| Capability | Status in UWP Dev Mode |
| --- | --- |
| Load an arbitrary executable or DLL from disk (`LoadLibrary` on an untrusted path, running a loose `.exe`) | **Forbidden.** Code must be part of the signed app package. |
| Create a process (`CreateProcess` and equivalents) | **Forbidden.** A UWP app cannot spawn another program. |
| JIT compilation (generate native code at runtime and execute it) | **Forbidden.** Executable memory must come from the package; dynamic code generation is not permitted. |
| Broad file system access | **Consent-gated.** The `broadFileSystemAccess` capability requires explicit user consent and is restricted on Xbox; the normal model is the app's own storage plus user-picked locations. |
| Win32 APIs generally | A subset only (the APIs UWP exposes). Most of the surface a desktop game expects is absent. |

These are not bugs to work around or policies that an entitlement request
removes. They are the platform working as designed, and any honest design for
X-Native has to start from them.

## What that means for the roadmap

- **Phase 1 — Foundation.** Fully possible, and this is where the project
  is: architecture, core libraries, the UWP host, logging, configuration,
  and the plugin framework all live comfortably inside the sandbox.
- **Phase 2 — Compatibility layer.** The classic Wine approach — load the
  game's PE, emulate Win32, run its code — is blocked by the first three
  rows of the table. What remains possible inside the sandbox is analysis
  (parsing and inspecting executables), translation done *ahead of time*,
  and hosting runtimes that are themselves part of the app package.
- **Phase 3 — Graphics.** A graphics backend that is compiled into the app
  package is possible; translating a running game's draw calls is only
  meaningful once something can run the game (see below).
- **Phase 4 — Input & audio.** Reading the Xbox controller and playing audio
  from inside the app are supported UWP capabilities. Mapping them onto a
  game's expectations matters once a game is running under one of the
  research directions below.
- **Phase 5 — Game support.** Profiles, a compatibility database, the
  launcher UI, and save management are all possible today, and are useful
  even while execution research continues: they are how the project records
  what is known about each game.

## What X-Native does today

- Catalogues games from disk, including `game.json` manifests
  ([game-manifest.md](game-manifest.md)).
- Analyses PE executables (headers, architecture, characteristics) with
  an inspector in `XNative.Platform`, and classifies every catalogued
  executable with it: a valid PE image's architecture is recorded on the
  game, while a file that is not a valid PE image (or cannot be read) is
  catalogued with `Unknown` architecture rather than failing the scan.
- Reads the Xbox gamepad through `Windows.Gaming.Input` and feeds it into
  the platform's input service, so the launcher's input contracts reflect
  the real controller — input is ready for a game the day one can run.
- Stores compatibility profiles (`GameProfile`, `CompatibilityRating`)
  through `IGameProfileStore`, implemented by an in-memory store and a
  JSON-backed store in `XNative.Wine`. The default registration ships an
  empty store — no curated profile database ships yet — but any profile
  that is present is resolved and applied on every launch attempt.
- Provides the launcher UI (scan, search, sort, launch) and reports module
  health on the Xbox host's startup screen.
- Attempts launches through `IGameRunner` and the Wine compatibility
  layer, which share one launch pipeline: the request is validated, the
  profile is consulted, and the attempt ends in a structured failure
  (`LaunchResult` with a `LaunchFailureReason`) instead of executing the
  game. The backend that would actually run a game does not exist.

**No PC game runs through X-Native today.** Any source claiming otherwise
is wrong, and a bug report against the claim is welcome.

## Compatibility ratings

Every profile rates its game with a `CompatibilityRating`:

| Rating | Meaning |
| --- | --- |
| `Unknown` | Nobody has reported this game yet. |
| `Broken` | The game does not start, or crashes immediately. |
| `Starts` | The game starts but cannot really be played. |
| `Playable` | The game can be played, with noticeable issues. |
| `Perfect` | The game works as well as on a PC. |

Ratings today are knowledge, not results: no game has been run, so a
`Playable` or `Perfect` rating records what a profile author believes a
future backend would achieve — it is never evidence that the game ran.

Profiles are matched to games by executable file name (case-insensitive,
directory ignored). `WineCompatibilityLayer.CanRun(game)` is a cheap
prediction taken straight from the database: it returns the matching
profile's rating, and `Unknown` when the game has no profile.

## Why a launch fails: the structured reasons

A launch attempt walks a fixed pipeline and stops at the first failure.
The reasons a caller can observe today:

| `LaunchFailureReason` | When it happens |
| --- | --- |
| `UnsupportedExecutableType` | The catalogued entry is not a Windows executable. Checked first, before anything else. |
| `ExecutableNotFound` | The executable file is not on disk (checked when a file system service is available). |
| `KnownBroken` | The game's profile rates it `Broken`. The failure message includes the profile's notes when it has any. |
| `BackendNotImplemented` | The request was valid and not known-broken — and there is still no backend to run it. This is where every otherwise-valid launch ends today. When a profile matched, the message names its rating. |

The enum also defines reasons the pipeline does not currently produce —
`UnsupportedArchitecture`, `InvalidExecutable`, `BlockedBySandbox`,
`Cancelled`, plus `None` (success) and `Unknown` — reserved for the
backends and checks that will one day need them.

## Credible research directions

Ordered roughly by how directly they engage with the sandbox constraints:

1. **Static recompilation.** Translate a game's binary ahead of time —
   offline, on a PC — into something that can be packaged and signed as
   part of an app. This respects the "code comes from the package" rule by
   construction. It is also a very large undertaking per game, and is a
   research topic, not a feature in progress.
2. **Embedded runtimes.** Some games are not native code at all: they run on
   an interpreter or a VM (Java games are the classic example). A runtime
   that is itself compiled into the app package — in the style of the
   Java/Bandit Launcher projects — can host such a game without loading
   arbitrary native code or JIT-compiling native code. This is the most
   plausible route to a first genuinely running title.
3. **Streaming from a PC.** The game runs on a PC the user owns; the Xbox
   displays it and sends controller input back, in the style of
   Moonlight/GameStream. This works within every platform rule because
   nothing foreign executes on the console. It is also, honestly, a
   streaming client rather than a compatibility layer — but it is a real
   way to play PC games on the console.
4. **Managed / .NET games.** Games written for .NET can, in principle, be
   recompiled or IL-translated into a form a UWP app may execute, since the
   target runtime already exists on the platform. Per-game effort varies
   enormously, and native dependencies in an otherwise managed game break
   the approach.

Directions that are **not** credible under the current platform rules
include porting Wine/Box64 as-is (both need process creation, arbitrary
DLL loading, and JIT), and any design whose first step is "get the sandbox
restriction lifted."

## How to help

- Test and report what the current build actually does with real games,
  using the game compatibility issue template.
- Contribute PE test vectors and compatibility profiles (see
  [CONTRIBUTING.md](../CONTRIBUTING.md)).
- Research write-ups that engage with the constraints above — especially
  per-game analysis of whether a title is native, managed, or
  interpreter-hosted — move the project further than speculative
  architecture proposals.
