## ADDED Requirements

### Requirement: Relay preserves direct graphs and prepares binding copies structurally

Relay SHALL execute an empty-binding graph from the selected path without rewriting it. When supplied bindings require a mutable graph, Relay SHALL create a JSON-aware temporary copy that preserves the source run mode and does not modify the source file.

#### Scenario: Manual graph is executed directly
- **WHEN** a valid graph has no supplied input bindings
- **THEN** Relay passes the selected graph path to DynamoRevit without rewriting the graph
- **AND** the source graph remains byte-for-byte unchanged

#### Scenario: Manual graph is prepared for binding
- **WHEN** a valid graph with top-level `RunType` set to `Manual` requires a temporary binding copy
- **THEN** the temporary copy retains top-level `RunType` set to `Manual`
- **AND** the source graph remains byte-for-byte unchanged

#### Scenario: Graph JSON is invalid
- **WHEN** a graph required for staged binding cannot be parsed as valid Dynamo JSON
- **THEN** Relay returns a failed execution outcome with the graph path and parse context
- **AND** Dynamo is not invoked

### Requirement: Dynamo compatibility is validated before invocation

Relay MUST validate the required DynamoRevit assembly, types, staged execution members, input-binding members, and signatures for the active supported Revit version before using the relevant graph execution stage.

#### Scenario: Compatible DynamoRevit is loaded
- **WHEN** all required adapter bindings are available
- **THEN** Relay loads, binds, and invokes the graph using the adapter for the active Revit version

#### Scenario: DynamoRevit is unavailable or incompatible
- **WHEN** any required binding is missing or has an incompatible signature
- **THEN** Relay returns a failed command result
- **AND** reports the missing compatibility boundary without a null-reference failure

### Requirement: Graph execution selects a direct or staged one-shot path

Relay SHALL use DynamoRevit's supported UI-less execution command for empty bindings and SHALL expose graph preparation, paused load, typed binding, and evaluation as ordered stages when bindings are supplied.

#### Scenario: Graph is loaded for direct execution
- **WHEN** a selected graph has no supplied input bindings
- **THEN** Relay forces manual paused load and temporarily selects synchronous scheduler processing
- **AND** does not enable Dynamo automation/test mode
- **AND** resets and attaches the engine without dirtying nodes, marks every loaded node for forced execution, and calls workspace `Run()` exactly once before restoring the prior scheduler mode

#### Scenario: Graph is loaded for input binding
- **WHEN** a caller supplies validated typed bindings keyed by Dynamo node GUID
- **THEN** Relay loads the prepared graph without evaluation
- **AND** applies every binding before requesting evaluation

#### Scenario: Binding target is invalid
- **WHEN** a supplied node GUID is missing or does not have the expected runtime node identity
- **THEN** Relay returns a failed binding outcome with node and adapter context
- **AND** does not evaluate the graph

#### Scenario: Binding application fails
- **WHEN** any adapter cannot apply a supplied value through its validated binding surface
- **THEN** Relay reports the binding failure separately from invocation
- **AND** does not evaluate the graph

#### Scenario: Execution is cancelled before evaluation
- **WHEN** the caller cancels after preparation or load but before evaluation
- **THEN** Relay returns a cancelled execution outcome
- **AND** performs no graph evaluation
- **AND** disposes the staged execution session and temporary graph

### Requirement: Dynamo model lifecycle follows the active document

Relay SHALL reuse or shut down Dynamo model state according to the last successful execution document and the active version adapter.

#### Scenario: Consecutive run uses the same document
- **WHEN** a graph runs after a successful run in the same live Revit document
- **THEN** Relay applies the adapter's same-document reuse policy

#### Scenario: Active document changes
- **WHEN** a graph runs in a different document than the last successful run
- **THEN** Relay requests the adapter's safe model transition before execution

#### Scenario: Execution fails
- **WHEN** graph invocation does not complete successfully
- **THEN** Relay MUST NOT record that document as the last successful execution document

#### Scenario: Execution is cancelled before evaluation
- **WHEN** a staged execution session is cancelled before graph evaluation
- **THEN** Relay MUST NOT record that document as the last successful execution document

### Requirement: Temporary execution files are cleaned up

Relay SHALL attempt to delete every temporary graph copy created for staged binding on success, failure, or cancellation.

#### Scenario: Invocation exits
- **WHEN** an invocation using a temporary binding copy returns or throws
- **THEN** Relay attempts temporary-file deletion
- **AND** reports a cleanup failure separately from the graph execution outcome

#### Scenario: Staged session exits before invocation
- **WHEN** graph load, binding, or caller cancellation ends a session before evaluation
- **THEN** Relay closes owned session state and attempts temporary-file deletion
- **AND** reports cleanup failures separately from the load, binding, or cancellation outcome
