## 1. Configuration Boundary

- [ ] 1.1 Add a typed graph-root resolution result and pure resolver for default, custom, empty, malformed, missing, and inaccessible settings
- [ ] 1.2 Add or reuse a Relay test project and cover path normalization, explicit default, invalid fallback, and diagnostics
- [ ] 1.3 Decide and document relative-path behavior before implementing it

## 2. Startup and Discovery

- [ ] 2.1 Refactor `App.OnStartup` to resolve configuration before directory enumeration
- [ ] 2.2 Update `Globals` so graph and local-icon paths derive from current resolved root/tab state
- [ ] 2.3 Replace broad configuration/discovery handling with scoped trace context and one concise Revit startup warning
- [ ] 2.4 Remove unused `ResetRibbon` state or define it through a separate specification
- [ ] 2.5 Verify valid local, missing, inaccessible, and temporarily unavailable roots in Revit 2025, 2026, and 2027

## 3. Validation

- [ ] 3.1 Run focused graph-root and discovery tests
- [ ] 3.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 3.3 Run strict OpenSpec validation and document configuration migration behavior
