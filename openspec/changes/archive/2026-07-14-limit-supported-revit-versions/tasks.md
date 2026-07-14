## 1. Build Matrix Cleanup

- [x] 1.1 Remove Debug and Release R21-R24 configurations from `src/Relay.sln` and `src/Relay.csproj`
- [x] 1.2 Remove R21-R24 target frameworks, Dynamo/Revit package selections, constants, and unreachable conditional code
- [x] 1.3 Audit and remove legacy `src/Relay.Revit20xx` folders after confirming supported targets do not consume them
- [x] 1.4 Keep explicit, reviewed target framework and DynamoRevit values for R25, R26, and R27

## 2. Packaging and Documentation

- [x] 2.1 Update CI/build automation to cover Debug R25, Debug R26, and Debug R27
- [x] 2.2 Verify Release R25, Release R26, and Release R27 produce only `_Release/Revit2025`, `Revit2026`, and `Revit2027` outputs
- [x] 2.3 Update support documentation with the 2025-2027 matrix and the historical release for Revit 2021-2024 users

## 3. Validation

- [x] 3.1 Restore and build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [x] 3.2 Build `src/Relay.sln` with `Release R25`, `Release R26`, and `Release R27` and inspect packaged artifacts
- [x] 3.3 Confirm solution/project configuration enumeration contains no R21-R24 entries
- [x] 3.4 Run strict OpenSpec validation and record the breaking compatibility note
