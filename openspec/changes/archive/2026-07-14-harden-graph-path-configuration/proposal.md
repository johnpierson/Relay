## Why

Relay reads an unchecked INI path and immediately enumerates it during startup. Missing, empty, inaccessible, or malformed paths can prevent the add-in from loading, while graph-resource paths can remain bound to the default tab instead of the selected graph root.

## Problem

Configuration parsing, path normalization, validation, fallback behavior, and graph discovery are interleaved in `App.OnStartup`, and startup failures are not surfaced consistently.

## Scope

- Normalize and validate configured graph roots before directory enumeration.
- Use the executing directory only for the explicit `default` setting or a clearly reported invalid configuration fallback.
- Compute graph and icon locations from the resolved configuration at access time.
- Keep startup functional when the configured root is empty, missing, or inaccessible.

## Non-goals

- Replacing the INI format with a new settings UI or storage system.
- Changing the tab/panel folder convention.
- Adding network-drive retry or credential management.

## What Changes

- Introduce a testable graph-root resolver with explicit outcomes.
- Separate settings reads from filesystem discovery.
- Report invalid configuration through Relay's established diagnostics and a concise Revit-facing notification when appropriate.
- Remove unused or misleading settings state such as an unread `ResetRibbon` value unless it gains defined behavior.

## Capabilities

### New Capabilities

- `graph-root-configuration`: Defines graph-root resolution, validation, fallback, and diagnostics.

### Modified Capabilities

None.

## Risks

- Existing installations may rely on accidental whitespace or relative-path behavior.
- Network paths may be temporarily unavailable during Revit startup.

## Compatibility

Applies uniformly to Revit 2025, 2026, and 2027 and does not depend on Dynamo versions.

## Impact

Affects `src/App.cs`, `src/Utilities/Globals.cs`, `src/Classes/RelayIniFile.cs`, graph discovery call sites, and new path-resolution tests.
