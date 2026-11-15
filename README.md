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
- [ ] Plugin framework — contracts only so far (`IPlugin`, `IPluginCatalog`, `IPluginHost`); no plugin is discovered or loaded from disk yet

---

## Phase 2 — Compatibility Layer

- [ ] Win32 abstraction
- [ ] Process management
- [ ] Memory management
- [ ] Threading
- [ ] DLL loading
- [ ] File system virtualization
- [x] PE-based executable analysis — a standalone inspector reads PE headers (architecture, subsystem, DLL/managed detection); the catalog does not use it yet

---

## Phase 3 — Graphics

- [ ] Graphics abstraction — the backend contract is defined in the core; no backend is implemented yet
- [ ] DirectX translation research
- [ ] Shader pipeline
- [ ] Texture management

---

## Phase 4 — Input & Audio

- [ ] Xbox controller support
- [ ] Keyboard & mouse abstraction
- [ ] Audio backend
- [ ] XAudio compatibility research

---

## Phase 5 — Game Support

- [ ] Game profiles — the profile model and store contract are defined; no profile store or database yet
- [ ] Compatibility database
- [ ] Launcher UI
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

To be precise about what works today: X-Native can catalogue games from disk (including `game.json` manifests) and present them in the launcher UI, a standalone inspector can analyse a game's executable (PE headers, architecture, subsystem), and the Xbox host reports module health on its startup screen. Compatibility profiles are modelled in the core but are not yet stored or applied at runtime. **No PC game runs through X-Native yet** — the UWP sandbox forbids loading arbitrary executables, spawning processes, and JIT compilation, so execution is a research problem, not a shipped feature. See [docs/compatibility.md](docs/compatibility.md) for the full, honest picture.

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
