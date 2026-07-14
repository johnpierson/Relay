# Spec-Driven Development Specification

## Purpose

Define how Relay records, validates, implements, and preserves behavioral changes
with OpenSpec.

## Requirements

### Requirement: Behavioral changes use an OpenSpec change

Relay maintainers SHALL record a planned user-visible behavior change in an
OpenSpec change before treating its implementation as complete.

#### Scenario: A behavior change is proposed

- **WHEN** a maintainer proposes new or changed Relay behavior
- **THEN** the repository contains a change with a proposal, delta specs, design, and implementation tasks

#### Scenario: Exploration has not reached a decision

- **WHEN** a maintainer is investigating a problem without committing to a behavior change
- **THEN** the investigation MAY remain in exploration without creating normative requirements

### Requirement: OpenSpec artifacts pass strict validation

The repository SHALL validate all OpenSpec changes and specifications in strict,
noninteractive mode when OpenSpec files or their validation workflow change.

#### Scenario: A pull request changes an OpenSpec artifact

- **WHEN** a pull request modifies `.codex/`, `openspec/`, or the OpenSpec validation workflow
- **THEN** continuous integration runs the repository-pinned OpenSpec validator
- **AND** the check fails when an artifact violates the OpenSpec schema

### Requirement: Completed changes update the source specifications

Maintainers SHALL reconcile implemented behavior with the capability
specifications before a change is considered complete.

#### Scenario: Implementation and verification are complete

- **WHEN** all implementation tasks and required verification for a change are complete
- **THEN** the change is archived
- **AND** its delta specifications are reflected in `openspec/specs/`

### Requirement: Relay compatibility is explicit

Every behavior change that depends on Revit or Dynamo versions MUST identify its
compatibility assumptions for the supported Revit 2025-2027 product matrix.

#### Scenario: Host behavior differs by version

- **WHEN** a proposed change uses version-specific Revit or Dynamo APIs
- **THEN** its proposal and design identify each affected supported Revit version
- **AND** its tasks include verification for each affected supported configuration
