## ADDED Requirements

### Requirement: Synchronization discovers a complete graph snapshot

Relay SHALL discover normalized `.dyn` paths grouped by their configured tab and panel before changing ribbon state.

#### Scenario: Discovery succeeds
- **WHEN** the configured graph tree can be enumerated
- **THEN** Relay produces one complete snapshot of applicable graphs for the active Revit version

#### Scenario: Discovery fails
- **WHEN** an applicable directory cannot be enumerated
- **THEN** Relay reports the failure
- **AND** Relay MUST NOT hide controls based on an incomplete snapshot

### Requirement: Repeated synchronization is idempotent

Relay SHALL preserve existing visible controls when a synchronization snapshot contains the same graph paths.

#### Scenario: Filesystem is unchanged
- **WHEN** synchronization runs repeatedly without graph changes
- **THEN** each graph remains represented by one active ribbon mapping
- **AND** its panel remains visible

### Requirement: Synchronization reconciles additions and removals

Relay SHALL add newly discovered graphs and hide only controls whose mapped graph path is absent from a complete snapshot.

#### Scenario: Graph is added
- **WHEN** a new `.dyn` file appears in a discovered panel directory
- **THEN** synchronization creates an active button mapping for that graph
- **AND** the containing panel is visible

#### Scenario: Graph is removed
- **WHEN** a previously mapped graph is absent from a complete snapshot
- **THEN** synchronization hides that graph's control
- **AND** removes its path from active graph lookup after reconciliation

### Requirement: Panel visibility reflects complete membership

Relay SHALL evaluate panel visibility from all tracked controls in that panel rather than only controls created by the latest synchronization.

#### Scenario: Panel retains a valid graph
- **WHEN** at least one graph mapped to a panel remains active
- **THEN** the panel remains visible

#### Scenario: Panel has no valid graphs
- **WHEN** every graph mapped to a panel is inactive after reconciliation
- **THEN** Relay hides the panel
