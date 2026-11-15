## Summary

<!-- What does this change, and why? Link the issue it addresses, if any. -->

## Testing

- [ ] `dotnet build X-Native.sln -c Release` succeeds with no warnings
- [ ] `dotnet test X-Native.sln -c Release` passes
- [ ] New or changed behaviour is covered by tests, or this change needs none (say why)
- [ ] If this touches the Xbox host (`XNative.XboxHost`): built in Visual Studio / MSBuild for `x64` and deployed to a Dev Mode console — or this PR does not touch the host

## Scope & honesty

- [ ] This change stays within its stated scope (no unrelated refactors or drive-by reformatting)
- [ ] Architecture rules hold: `XNative.Core` gains no dependencies, modules do not reference each other, wiring happens only in a composition root
- [ ] No documentation, comment, or UI text in this change claims behaviour that is not implemented and verified — in particular, nothing claims that X-Native can run a game unless that was actually demonstrated
- [ ] Public API changes have XML documentation comments
