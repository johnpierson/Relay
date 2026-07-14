## Why

Relay's product policy supports Revit 2025-2027, but the solution and project still advertise and build Revit 2021-2024 configurations. Keeping unsupported targets increases restore/build complexity, preserves dead conditional code, and can produce release artifacts that appear supported.

## Problem

The declared build matrix, source constants, legacy project folders, package conditions, and release output do not match the supported product matrix.

## Scope

- Remove Revit 2021-2024 solution and project configurations.
- Remove obsolete conditional compilation and legacy project artifacts.
- Ensure build, packaging, documentation, and CI identify only Revit 2025-2027.
- Preserve the correct target framework and Dynamo dependency per supported Revit version.

## Non-goals

- Migrating user settings or graph files from older Relay installations.
- Guaranteeing graph compatibility across Dynamo major versions.
- Changing ribbon or graph execution behavior.

## What Changes

- **BREAKING**: Revit 2021, 2022, 2023, and 2024 are no longer buildable or distributed.
- Reduce the MSBuild configuration matrix to Debug/Release R25, R26, and R27.
- Remove unreachable legacy code and folders after verifying no supported target consumes them.
- Add matrix validation for all six supported configurations and release folders.

## Capabilities

### New Capabilities

- `revit-version-support`: Defines Relay's supported Revit versions, build configurations, target frameworks, and release outputs.

### Modified Capabilities

None.

## Risks

- Downstream users building old configurations will need to remain on an earlier Relay release.
- Revit 2027 dependencies and target framework assumptions may evolve before final Autodesk releases.

## Compatibility

Supported hosts are Revit 2025, 2026, and 2027 only. Their configured Dynamo versions remain version-specific.

## Impact

Affects `src/Relay.sln`, `src/Relay.csproj`, legacy `src/Relay.Revit20xx` folders, conditional code, `_Release`, CI, and support documentation.
