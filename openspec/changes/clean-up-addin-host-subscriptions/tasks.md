## 1. Lifecycle Ownership

- [ ] 1.1 Add explicit subscription ownership state to `App` with guarded subscribe and detach methods
- [ ] 1.2 Pair `UIElementActivated`, `AssemblyResolve`, and `ApplicationInitialized` subscriptions with reverse-order cleanup
- [ ] 1.3 Implement `Globals.ResetSessionState` for mutable ribbon, command, and document/model references

## 2. Failure Handling and Tests

- [ ] 2.1 Make `OnShutdown` continue cleanup after an individual detach failure and trace each failure with event context
- [ ] 2.2 Add or reuse a Relay test project with a lifecycle coordinator test double for full, partial, duplicate, and repeated startup/shutdown
- [ ] 2.3 Verify one handler invocation per event and no stale Relay callbacks after shutdown in Revit 2025, 2026, and 2027

## 3. Validation

- [ ] 3.1 Run focused lifecycle ownership tests
- [ ] 3.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 3.3 Run strict OpenSpec validation and record host teardown results
