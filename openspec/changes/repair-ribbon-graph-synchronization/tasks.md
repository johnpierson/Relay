## 1. State Model

- [ ] 1.1 Add typed graph snapshot, tab-and-panel identity, reconciliation plan, and active/historical registry models
- [ ] 1.2 Extract complete normalized graph discovery and pure snapshot comparison from `RibbonUtils.SyncGraphs`
- [ ] 1.3 Extend `tests/Relay.Tests` for unchanged, added, removed, restored, duplicate-name, same-panel-name, and failed-discovery snapshots

## 2. Ribbon Reconciliation

- [x] 2.1 Append every successfully created control to cumulative panel membership without replacing existing entries
- [ ] 2.2 Replace per-file `File.Exists` cleanup with snapshot-based two-phase reconciliation applied only after complete discovery
- [ ] 2.3 Replace tooltip-derived synchronization identity with normalized Relay-owned graph, tab, panel, and host-control mappings
- [ ] 2.4 Reactivate reusable historical controls and derive panel visibility from complete tab-and-panel membership
- [ ] 2.5 Verify repeated Sync, add, remove, restore, rename, duplicate panel names, and empty-panel behavior in Revit 2025, 2026, and 2027

## 3. Validation

- [ ] 3.1 Run focused reconciliation tests
- [ ] 3.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 3.3 Run strict OpenSpec validation and record host-test results in the pull request
