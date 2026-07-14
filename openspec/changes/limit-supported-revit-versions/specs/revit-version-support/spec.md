## ADDED Requirements

### Requirement: Relay supports only Revit 2025 through 2027

Relay SHALL declare Revit 2025, Revit 2026, and Revit 2027 as its complete supported host matrix.

#### Scenario: Supported versions are inspected
- **WHEN** a maintainer reviews project configurations and support documentation
- **THEN** only Revit 2025, 2026, and 2027 are identified as supported

#### Scenario: Legacy version is requested
- **WHEN** a user requires Revit 2021, 2022, 2023, or 2024
- **THEN** current Relay builds do not claim compatibility
- **AND** documentation directs the user to an appropriate historical release

### Requirement: Every supported host has explicit build configuration

Relay SHALL provide Debug and Release configurations with explicit target framework, Revit API package, and DynamoRevit package selection for each supported Revit version.

#### Scenario: Supported debug matrix builds
- **WHEN** `Debug R25`, `Debug R26`, and `Debug R27` are built
- **THEN** each configuration resolves only its intended host and Dynamo dependencies

#### Scenario: Supported release matrix builds
- **WHEN** `Release R25`, `Release R26`, and `Release R27` are built
- **THEN** each configuration produces a Relay binary for its intended host

### Requirement: Unsupported configurations are absent

The solution and project MUST NOT expose build configurations or conditional source branches for Revit 2021-2024.

#### Scenario: Build matrix is enumerated
- **WHEN** solution and project configurations are listed
- **THEN** no R21, R22, R23, or R24 configuration is present

### Requirement: Release output matches the support matrix

Relay SHALL package supported binaries only under version-specific Revit 2025, 2026, and 2027 release locations.

#### Scenario: Release artifacts are assembled
- **WHEN** all supported Release configurations complete
- **THEN** the release tree contains outputs for Revit 2025, 2026, and 2027
- **AND** contains no newly generated outputs for unsupported Revit versions
