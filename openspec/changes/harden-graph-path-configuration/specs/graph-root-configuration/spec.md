## ADDED Requirements

### Requirement: Relay resolves the configured graph root

Relay SHALL normalize and validate the configured graph root before graph discovery begins.

#### Scenario: Default root is configured
- **WHEN** the `Path` setting equals `default` without regard to case
- **THEN** Relay uses its executing directory as the graph root

#### Scenario: Valid custom root is configured
- **WHEN** the `Path` setting identifies an accessible directory
- **THEN** Relay uses the normalized custom directory as the graph root

### Requirement: Invalid configuration does not prevent startup

Relay SHALL keep the add-in available when the configured graph root is empty, malformed, missing, or inaccessible.

#### Scenario: Configured root is invalid
- **WHEN** graph-root validation fails during startup
- **THEN** Relay uses the documented fallback root
- **AND** emits a diagnostic containing the rejected setting and reason
- **AND** presents no more than one concise startup warning to the user

### Requirement: Discovery failures preserve existing ribbon state

Relay MUST NOT interpret a graph-root enumeration failure as an empty successful graph set.

#### Scenario: Root becomes unavailable during sync
- **WHEN** Relay cannot enumerate the resolved graph root
- **THEN** Relay reports the failure
- **AND** leaves currently active ribbon mappings visible

### Requirement: Derived graph resources follow resolved configuration

Relay SHALL derive tab, graph, and local icon paths from the current resolved root and tab state when those paths are requested.

#### Scenario: Custom root selects a tab
- **WHEN** Relay resolves a custom root and selects a graph tab
- **THEN** graph discovery and local setup-icon lookup use paths derived from that resolved state
