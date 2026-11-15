# X-Native

> **Run PC games on Xbox Dev Mode.**
>
> An open-source compatibility layer and launcher that aims to bring Steam, Epic Games, and other Windows PC games to Xbox consoles running Developer Mode.

> **Status:** 🚧 Early Development

[![CI](https://github.com/Arshiazxx/X-Native/actions/workflows/ci.yml/badge.svg)](https://github.com/Arshiazxx/X-Native/actions/workflows/ci.yml)

---

# What is X-Native?

X-Native is an experimental compatibility platform designed to make running Windows PC games on **Xbox Dev Mode (UWP)** possible.

Rather than emulating a PC, X-Native aims to provide a compatibility environment that allows supported games to run through native translation layers and platform abstractions.

The project is inspired by the philosophy behind projects like Wine, Proton, DXVK, and Box86/64—but is built specifically for Xbox Developer Mode.

---

# Vision

Imagine launching:

- 🎮 Steam Games
- 🎮 Epic Games Store Games
- 🎮 DRM-Free Windows Games
- 🎮 Fan Games
- 🎮 Indie Titles

...directly from an Xbox Dev Mode application.

That's the long-term vision.

---

# Goals

- Native UWP launcher
- Windows API compatibility layer
- Graphics abstraction
- Input translation
- Audio translation
- File system virtualization
- Plugin architecture
- Game profiles
- Performance-focused design
- Open-source from day one

---

# Planned Architecture

```
                 Xbox Dev Mode
                       │
                X-Native Launcher
                       │
        ┌──────────────┴──────────────┐
        │                             │
 Compatibility Layer           Plugin System
        │                             │
 ┌──────┴────────┐           ┌────────┴────────┐
 │               │           │                 │
Graphics      Audio       Community Plugins  Tools
 │               │
 Input      File System
 │               │
 └───────────────┴───────────────┐
                                 │
                          Windows PC Games

```

---

# Current Roadmap

## Phase 1 — Foundation

- [x] Project architecture
- [x] Core libraries
- [x] UWP host application
- [x] Logging system
- [x] Configuration system (options-based: catalog, logging, and storage are configured through options classes)
- [x] Persisted settings (validated `AppSettings` over a JSON settings service) and a diagnostic console CLI in the development head
- [x] Plugin framework — `PluginHost`, an in-memory `IPluginCatalog`, and a failure-isolating `PluginManager` are implemented; plugins are compiled-in and registered by a head, because the sandbox forbids loading assemblies from disk at runtime

---

## Phase 2 — Compatibility Layer

- [ ] Win32 abstraction
- [ ] Process management
- [ ] Memory management
- [ ] Threading
- [ ] DLL loading
- [ ] File system virtualization
- [x] PE-based executable analysis — an inspector reads PE headers (architecture, subsystem, DLL/managed detection), and the catalog classifies every executable it records with it

---

## Phase 3 — Graphics

- [ ] Graphics abstraction — the core contract, a backend registry, and a capability-based selector exist, with a null backend that renders nothing; no real rendering backend is implemented yet
- [ ] DirectX translation research
- [ ] Shader pipeline
- [ ] Texture management

---

## Phase 4 — Input & Audio

- [x] Xbox controller support — gamepad snapshot contracts in the core, a host-fed input service in the platform, and an Xbox host feed polling `Windows.Gaming.Input` at ~60 Hz
- [ ] Keyboard & mouse abstraction — keyboard state contracts and service support exist; no host feeds keyboard input yet, and there is no mouse model
- [ ] Audio backend
- [ ] XAudio compatibility research

---

## Phase 5 — Game Support

- [ ] Game profiles — the profile model and in-memory/JSON profile stores are implemented and resolved at launch; an examples pack ships three sample profiles, but no curated profile data ships yet
- [ ] Compatibility database
- [ ] Launcher UI — view models cover scanning, search/filter, sorting, and launching, and the Xbox host ships a working (minimal) page over them; no polished or desktop views ship yet
- [ ] Save management

---

# Current Status

This project is **very early** in development.

Expect:

- Frequent API changes
- Experimental code
- Incomplete features
- Breaking changes

Nothing should currently be considered stable.

To be precise about what works today: X-Native can catalogue games from disk (including `game.json` manifests) and present them in the launcher UI, every catalogued executable is classified from its PE headers (architecture, subsystem, DLL/managed), the Xbox host reports module health on its startup screen and polls the real Xbox controller into the platform's input service, and a diagnostic console CLI (`info` / `scan` / `inspect` / `launch` / `settings`) exercises the same composition root on a PC, with persisted settings applied to the catalog and logging. Compatibility profiles are stored (in-memory and JSON-backed stores) and resolved on every launch attempt — a game whose profile rates it broken is refused with a structured `KnownBroken` failure — but the default registration ships an empty store and no backend exists behind the launch pipeline. **No PC game runs through X-Native yet** — the UWP sandbox forbids loading arbitrary executables, spawning processes, and JIT compilation, so execution is a research problem, not a shipped feature. See [docs/compatibility.md](docs/compatibility.md) for the full, honest picture.

---

# Building

With the .NET SDK 8.0.x installed:

```bash
dotnet build X-Native.sln -c Release
dotnet test X-Native.sln -c Release
```

That builds and tests the managed solution on Windows or Linux. Building and deploying the Xbox host needs Visual Studio 2022 and a console in Developer Mode. Full instructions: [docs/building.md](docs/building.md). The `game.json` manifest format is documented in [docs/game-manifest.md](docs/game-manifest.md).

---

# Why Open Source?

Projects like this are too large for one developer alone.

By developing in the open, we hope to:

- Share knowledge
- Encourage experimentation
- Welcome contributors
- Build useful tools for the community
- Preserve compatibility research

---

# Contributing

Contributions of all sizes are welcome.

You don't need to be an emulator expert to help.

Ways to contribute include:

- Documentation
- Bug reports
- Testing
- C#
- C++
- UWP development
- Graphics research
- Reverse engineering
- Performance optimization
- Build systems
- CI/CD
- UI/UX improvements

If you're new to open source, beginner-friendly issues will be labeled accordingly.

---

# Principles

X-Native is built around a few core principles:

- 🟢 Open development
- 🟢 Clean architecture
- 🟢 Modular design
- 🟢 Transparency
- 🟢 Community collaboration
- 🟢 Long-term maintainability

---

# Disclaimer

X-Native is an independent open-source project and is **not affiliated with or endorsed by Microsoft, Valve, Epic Games, or any game publisher**.

Users are responsible for obtaining and using legally acquired copies of any games they run with X-Native.

---

# License

This project is licensed under the **MIT License**.

---

# Join the Journey

X-Native is an ambitious, long-term project.

There are no promises about what games will work or when. Progress will happen one milestone at a time, and every improvement—no matter how small—brings the project closer to its goal.

Whether you're here to contribute code, report bugs, test builds, improve documentation, or simply follow development, you're welcome. 🚀
