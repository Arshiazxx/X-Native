# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Real core contracts for the platform services — logging, storage,
  processes, the compatibility layer, the graphics backend, and the plugin
  host — replacing the member-less placeholder interfaces, together with
  launch options and compatibility game profiles.
- Platform service implementations, so a composition root can now construct
  the full service set, including the game catalog.
- Stable game IDs in the local game catalog, so a catalogued game keeps its
  identity across rescans.
- A PE inspector that reads the headers of Windows executables and reports
  their architecture and characteristics.
- A runner pipeline that resolves a game's compatibility profile before a
  launch attempt and reports structured launch results.
- Launcher search and launch wiring in the game library UI.
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
  disk, PE analysis, compatibility profiles, and the launcher UI — and that
  no game runs through X-Native yet.
