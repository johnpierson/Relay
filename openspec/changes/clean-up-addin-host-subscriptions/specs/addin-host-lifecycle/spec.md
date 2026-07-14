## ADDED Requirements

### Requirement: Relay owns each host subscription explicitly

Relay SHALL record ownership only after each startup event subscription succeeds and SHALL NOT register the same handler more than once per add-in instance.

#### Scenario: Startup completes
- **WHEN** Relay startup succeeds
- **THEN** exactly one Relay handler is registered for each required host event

#### Scenario: Startup is invoked while already initialized
- **WHEN** the same Relay application instance receives duplicate initialization
- **THEN** Relay MUST NOT add duplicate event handlers

### Requirement: Shutdown detaches owned subscriptions

Relay SHALL attempt to detach every event handler it owns during shutdown in reverse ownership order.

#### Scenario: Normal shutdown
- **WHEN** Revit calls Relay shutdown after successful startup
- **THEN** all Relay-owned host and AppDomain handlers are detached

#### Scenario: Partial startup is shut down
- **WHEN** startup fails after only some subscriptions succeed
- **THEN** shutdown detaches each subscription marked as owned
- **AND** does not attempt cleanup that depends on unavailable state

#### Scenario: One detachment fails
- **WHEN** a host event cannot be detached
- **THEN** Relay records the failure
- **AND** continues attempting the remaining cleanup steps

### Requirement: Shutdown clears session-scoped state

Relay SHALL release session-scoped mappings and pending command or document references during shutdown.

#### Scenario: Shutdown completes
- **WHEN** Relay finishes its cleanup sequence
- **THEN** active ribbon registries and pending graph command context are empty
- **AND** weak document/model lifecycle references are reset
