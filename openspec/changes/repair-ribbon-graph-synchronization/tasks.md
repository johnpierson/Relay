## 1. State Model

- [ ] 1.1 Add typed discovery snapshot and ribbon registry models under `src/Classes` or `src/Utilities`
- [ ] 1.2 Extract normalized graph discovery and snapshot comparison from `RibbonUtils.SyncGraphs`
- [ ] 1.3 Add or reuse a Relay test project and cover unchanged, added, removed, duplicate-name, and failed-discovery snapshots

## 2. Ribbon Reconciliation

- [ ] 2.1 Refactor `RibbonUtils.AddItems` to append successful controls to complete panel membership
- [ ] 2.2 Refactor `RibbonUtils.HideUnused` into a two-phase reconciliation that never mutates a collection during enumeration
- [ ] 2.3 Preserve graph metadata across Revit and Autodesk ribbon wrappers and restore valid panel visibility
- [ ] 2.4 Verify repeated Sync, add, remove, rename, and empty-panel behavior in Revit 2025, 2026, and 2027

## 3. Validation

- [ ] 3.1 Run focused reconciliation tests
- [ ] 3.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 3.3 Run strict OpenSpec validation and record host-test results in the pull request
