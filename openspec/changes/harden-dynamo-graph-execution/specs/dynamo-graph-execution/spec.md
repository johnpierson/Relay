## ADDED Requirements

### Requirement: Relay prepares a temporary automatic graph structurally

Relay SHALL create a temporary copy of the selected graph and set its top-level run mode to automatic through JSON-aware processing without modifying the source file.

#### Scenario: Manual graph is prepared
- **WHEN** a valid graph has top-level `RunType` set to `Manual`
- **THEN** the temporary copy has top-level `RunType` set to `Automatic`
- **AND** the source graph remains byte-for-byte unchanged

#### Scenario: Graph JSON is invalid
- **WHEN** the selected graph cannot be parsed as valid Dynamo JSON
- **THEN** Relay returns a failed execution outcome with the graph path and parse context
- **AND** Dynamo is not invoked

### Requirement: Dynamo compatibility is validated before invocation

Relay MUST validate the required DynamoRevit assembly, types, members, and signatures for the active supported Revit version before invoking a graph.

#### Scenario: Compatible DynamoRevit is loaded
- **WHEN** all required adapter bindings are available
- **THEN** Relay invokes the graph using the adapter for the active Revit version

#### Scenario: DynamoRevit is unavailable or incompatible
- **WHEN** any required binding is missing or has an incompatible signature
- **THEN** Relay returns a failed command result
- **AND** reports the missing compatibility boundary without a null-reference failure

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

### Requirement: Temporary execution files are cleaned up

Relay SHALL attempt to delete every temporary graph copy on success, failure, or cancellation.

#### Scenario: Invocation exits
- **WHEN** Dynamo invocation returns or throws
- **THEN** Relay attempts temporary-file deletion
- **AND** reports a cleanup failure separately from the graph execution outcome
