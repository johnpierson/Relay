## ADDED Requirements

### Requirement: Each graph control has a unique stable identity

Relay SHALL assign each graph ribbon control an identity derived from its normalized full graph path and independent of presentation text.

#### Scenario: Graph filenames are duplicated
- **WHEN** two discovered graphs have the same filename in different directories
- **THEN** Relay assigns distinct control identities and mappings to each graph

#### Scenario: Graph is synchronized again
- **WHEN** an unchanged graph path is discovered in a later synchronization
- **THEN** Relay derives the same graph identity

### Requirement: Activation resolves only registered Relay controls

Relay SHALL create executable graph context only when the activated control identity exists in Relay's active registry.

#### Scenario: Registered graph button is activated
- **WHEN** the user activates an active Relay graph control
- **THEN** Relay associates the resulting command context with exactly that control's graph path

#### Scenario: Unrelated control is activated
- **WHEN** an activation event belongs to another control or an inactive Relay mapping
- **THEN** Relay clears any prior graph command context
- **AND** MUST NOT select a graph for execution

### Requirement: Graph command context is single use

The Run command SHALL consume a validated graph context at most once.

#### Scenario: Valid activation launches Run
- **WHEN** `Relay.Run` executes after a registered graph activation
- **THEN** it consumes that graph path and clears the pending context

#### Scenario: Run has no valid context
- **WHEN** `Relay.Run` executes without a current registered activation
- **THEN** it returns a failed command result with an actionable message
- **AND** no graph is executed
