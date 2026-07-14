## 1. Identity and Registry

- [ ] 1.1 Add typed graph-control identity, graph mapping, and single-use command context models
- [ ] 1.2 Implement deterministic identity generation from normalized full graph paths
- [ ] 1.3 Add or reuse a Relay test project and cover duplicate filenames, stable identities, missing mappings, and consumed contexts

## 2. Host Routing

- [ ] 2.1 Register control identities only after successful button creation in `RibbonUtils`
- [ ] 2.2 Refactor `App.ComponentManagerOnUIElementActivated` to clear stale context and resolve only known Relay controls
- [ ] 2.3 Refactor `Run.Execute` to atomically consume validated context and return an actionable failure message when absent
- [ ] 2.4 Remove tooltip/description parsing and obsolete `Globals.CurrentGraphToRun` state
- [ ] 2.5 Verify activation ordering and identity properties in Revit 2025, 2026, and 2027

## 3. Validation

- [ ] 3.1 Run focused command-routing tests
- [ ] 3.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 3.3 Run strict OpenSpec validation and record host-test results
