## ADDED Requirements

### Requirement: Relay discovers declared graph inputs

Relay SHALL inspect a selected graph for Dynamo nodes marked `IsInput` and produce typed input descriptors keyed by node GUID before graph evaluation.

#### Scenario: Graph contains supported declared inputs
- **WHEN** a valid graph contains nodes marked `IsInput` whose concrete node identities are supported by the active adapter
- **THEN** Relay produces one descriptor for each declared input
- **AND** preserves its node GUID, display label, input kind, current value, and binding metadata

#### Scenario: Graph contains an unsupported declared input
- **WHEN** a node marked `IsInput` is not in the active adapter's supported-node matrix
- **THEN** Relay represents that node as unsupported with its node GUID and display label
- **AND** MUST NOT infer a writable input kind from display text or serialized value alone

#### Scenario: Graph has no declared inputs
- **WHEN** a valid graph contains no nodes marked `IsInput`
- **THEN** Relay bypasses the input dialog
- **AND** continues through the standard staged execution path with an empty binding set

#### Scenario: Input declaration cannot be parsed
- **WHEN** the graph JSON or a declared input's required identity is invalid
- **THEN** Relay returns a failed command outcome with graph and node context when available
- **AND** Dynamo evaluation does not begin

### Requirement: Relay presents and validates supported inputs

Relay SHALL generate a modal input dialog for declared graph inputs and SHALL validate supported values before applying any binding.

#### Scenario: User accepts valid values
- **WHEN** every supported required input has a valid value and no declared input is unsupported
- **THEN** Relay returns one typed binding per supported node GUID
- **AND** proceeds to input application

#### Scenario: Required value is invalid
- **WHEN** a supported required input is empty, out of range, or otherwise invalid for its descriptor
- **THEN** Relay identifies the invalid field in the dialog
- **AND** MUST NOT apply bindings or evaluate the graph

#### Scenario: Declared input is unsupported
- **WHEN** the dialog contains an unsupported declared input
- **THEN** Relay identifies the unsupported node and active compatibility boundary
- **AND** blocks graph execution

#### Scenario: User cancels input
- **WHEN** the user cancels or closes the input dialog
- **THEN** Relay returns `Result.Cancelled`
- **AND** applies no bindings, performs no graph evaluation, and cleans up owned execution resources

### Requirement: Relay supports the initial input compatibility matrix

Relay MUST support string, number, boolean, integer slider, number slider, file path, directory path, and Revit Categories inputs when the corresponding node identity is supported by the active adapter.

#### Scenario: Primitive or path input is accepted
- **WHEN** a user accepts a valid primitive, slider, or path value
- **THEN** Relay binds that value to the matching node GUID in the temporary graph
- **AND** leaves the source graph byte-for-byte unchanged

#### Scenario: Slider value violates its bounds
- **WHEN** a supplied slider value is outside the bounds declared by the graph input
- **THEN** Relay rejects the value before graph evaluation
- **AND** reports the permitted range

#### Scenario: Path value is invalid
- **WHEN** a required file or directory value does not satisfy the descriptor's path validation
- **THEN** Relay rejects the value before graph evaluation
- **AND** reports the affected input without modifying either graph file

### Requirement: Relay binds Revit Categories by stable identity

Relay SHALL populate a Categories input from the active Revit/Dynamo execution context and SHALL bind the selected category using a stable host identity rather than display text or dropdown position.

#### Scenario: Categories choices are available
- **WHEN** a supported Categories node is declared as input and the active adapter can enumerate its runtime choices
- **THEN** Relay displays the available localized category names
- **AND** retains a stable identity for each choice

#### Scenario: User selects a category
- **WHEN** the user accepts a category choice
- **THEN** the active adapter resolves its stable identity against the matching instantiated node GUID
- **AND** applies the corresponding runtime dropdown item before evaluation

#### Scenario: Category becomes stale
- **WHEN** the selected stable category identity is no longer available in the active execution context at binding time
- **THEN** Relay rejects the binding with node and category context
- **AND** MUST NOT fall back to a matching display name or dropdown index
- **AND** does not evaluate the graph

#### Scenario: Categories runtime surface is incompatible
- **WHEN** the active adapter cannot enumerate or select Categories items using its validated binding surface
- **THEN** Relay reports Categories as unsupported for that compatibility boundary
- **AND** does not evaluate the graph

### Requirement: Relay applies inputs before graph evaluation

Relay SHALL load the prepared graph without evaluation, apply all accepted input bindings, and evaluate the graph only after every binding succeeds.

#### Scenario: Every binding succeeds
- **WHEN** all accepted values match their expected node GUIDs and kinds and the active adapter applies all runtime bindings
- **THEN** Relay evaluates the graph once with the accepted inputs

#### Scenario: A binding target does not match
- **WHEN** a node GUID is missing or its concrete node identity no longer matches the discovered descriptor
- **THEN** Relay returns a failed execution outcome naming the affected input
- **AND** does not evaluate the graph

#### Scenario: Binding fails after graph load
- **WHEN** any JSON or runtime binding cannot be applied after the execution session is created
- **THEN** Relay does not evaluate the graph
- **AND** disposes the session and temporary graph according to the execution cleanup contract

### Requirement: Input behavior is version validated

Relay MUST validate input discovery and binding behavior for Revit 2025/Dynamo 3.0, Revit 2026/Dynamo 3.6, and Revit 2027/Dynamo 4.0 through isolated adapters.

#### Scenario: Supported adapter is active
- **WHEN** Relay runs a graph with declared inputs in a supported host version
- **THEN** it uses only node identities, serialized fields, and runtime members validated for that adapter

#### Scenario: Graph input differs across supported versions
- **WHEN** a node's serialization or runtime binding surface differs between supported Dynamo versions
- **THEN** each adapter translates that version into the same Relay input descriptor and binding outcome semantics

#### Scenario: Required version binding is missing
- **WHEN** an adapter lacks a required input member or compatible signature
- **THEN** Relay fails with an actionable compatibility diagnostic
- **AND** does not evaluate the graph
