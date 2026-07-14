## Context

Startup reads `RelaySettings.ini`, assigns its `Path` value directly to `Globals.BasePath`, and calls `Directory.GetDirectories` before validating the result. `Globals.RelayGraphs` is initialized once from the default ribbon tab, so later tab/root changes do not update resource lookup.

## Goals / Non-Goals

**Goals:**

- Resolve graph roots deterministically and safely before discovery.
- Distinguish explicit default configuration from invalid configuration.
- Keep startup available with useful diagnostics when a root cannot be used.

**Non-Goals:**

- Add a settings UI, replace INI storage, or manage network credentials.
- Change the graph folder convention.

## Decisions

1. Introduce a pure `GraphRootResolver` that accepts the raw setting and executing directory and returns a typed result containing normalized path, source, and warning. Returning a result is preferred over exceptions for expected invalid user configuration; unexpected I/O failures remain explicit.
2. Treat `default` case-insensitively as the only explicit default token. Empty, malformed, missing, or inaccessible paths use the executing directory only with a diagnostic, preventing silent configuration drift.
3. Enumerate directories through a discovery boundary that reports path and operation context. The alternative broad startup catch obscures whether settings, permissions, or graph JSON failed.
4. Replace cached derived paths such as `RelayGraphs` with methods/properties computed from resolved state at access time.

## Risks / Trade-offs

- [Temporarily unavailable network root falls back too quickly] -> Preserve a clear warning and allow explicit Sync to retry the configured root.
- [Relative paths currently work by accident] -> Define resolution against the settings file directory and test it, or reject them consistently before implementation is finalized.
- [Startup notification is noisy] -> Show at most one concise user-facing warning per startup and retain detailed trace diagnostics.

## Migration Plan

Existing valid absolute paths and `default` continue to work. Invalid values gain explicit fallback behavior. No settings file rewrite occurs automatically, so rollback requires only restoring the old resolver path.

## Open Questions

- Confirm whether relative paths should resolve against `Globals.ExecutingPath` or be rejected as unsupported.
