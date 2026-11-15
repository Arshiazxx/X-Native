## Summary

<!-- What does this change, and why? Link the issue it addresses, if any. -->

## Testing

- [ ] `dotnet build X-Native.sln -c Release` succeeds with no warnings (warnings are errors in this repo)
- [ ] `dotnet test X-Native.sln -c Release` passes — the solution references all eight test projects, so this runs the whole suite
- [ ] If this adds a test project: it is referenced by `X-Native.sln` and added to the `TEST_PROJECTS` list at the top of `.github/workflows/ci.yml` (CI also restores, builds, and tests every project in that list explicitly, and a guard job fails CI if a test project is in neither the solution nor the list)
- [ ] New or changed behaviour is covered by tests, or this change needs none (say why)
- [ ] If this touches the Xbox host (`src/XNative.XboxHost`): built with Visual Studio / MSBuild on Windows for `x64` — the dotnet CLI cannot build this project — with the deployment / startup-screen result noted if it was deployed to a Dev Mode console; or the host changes are explicitly marked as unverified in this PR description

## Scope & honesty

- [ ] This change stays within its stated scope (no unrelated refactors or drive-by reformatting)
- [ ] Architecture rules hold: `XNative.Core` gains no dependencies, modules do not reference each other, wiring happens only in a composition root
- [ ] No documentation, comment, or UI text in this change claims behaviour that is not implemented and verified — in particular, nothing claims that X-Native can run a game unless that was actually demonstrated
- [ ] Public API changes have XML documentation comments
