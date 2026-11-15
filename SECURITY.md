# Security Policy

## Supported versions

X-Native is pre-1.0 and under active development. Security fixes are applied
to the `main` branch only; there are no maintained release branches yet.

| Version | Supported |
| --- | --- |
| `main` (unreleased) | Yes |
| Any tagged pre-release | No — please retest against `main` |

## Reporting a vulnerability

Please report vulnerabilities privately using GitHub's private vulnerability
reporting for this repository:

**Repository → Security → Advisories → Report a vulnerability**
(<https://github.com/Arshiazxx/X-Native/security/advisories/new>)

Do not open a public issue, pull request, or discussion for a suspected
vulnerability. Include what you found, the steps to reproduce it, and the
version or commit you tested. You can expect an acknowledgement once a
maintainer has read the report; because this is a small volunteer project,
please allow reasonable time for a fix before any public disclosure.

## Scope

X-Native reads data it does not control, so parser and path-handling reports
are in scope and welcome — in particular:

- **`game.json` manifest parsing** — malformed, oversized, or adversarial
  manifests must fail cleanly, never crash the host or escape the manifest's
  folder.
- **Path handling** — manifest fields such as `executable` and
  `workingDirectory` are relative paths that must stay inside the manifest
  folder; a report showing a path traversal is a security issue.
- **Executable (PE) inspection** — X-Native parses untrusted PE files;
  crashes, hangs, or excessive memory use on a crafted file are in scope.
- **Catalogue scanning** — scanning a folder tree must not follow its way
  into unintended locations or stall indefinitely.

Out of scope: vulnerabilities in games themselves, in the Xbox operating
system or Device Portal, and in third-party dependencies (report those
upstream — though a note here is appreciated if X-Native is affected).
