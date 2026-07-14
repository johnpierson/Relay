# Relay OpenSpec workflow

Relay uses OpenSpec for spec-driven development. Requirements and decisions live
with the repository so a change can be reviewed before its implementation and
verified against the same contract afterward.

## Start a change

1. Restart Codex after cloning or pulling changes to `.codex/`.
2. Run `/opsx:propose` and describe the behavior to add or change.
3. Review the generated proposal, specs, design, and tasks under
   `openspec/changes/<change-name>/`.
4. Run `/opsx:apply` when the artifacts are ready for implementation.
5. Run `npx -y @fission-ai/openspec@1.6.0 validate --all --strict --no-interactive`.
6. Run `/opsx:archive` after the implementation and validation are complete.

`/opsx:explore` can be used before a proposal when the problem still needs
investigation. `/opsx:update` continues an incomplete change, and
`/opsx:sync` updates the main specs without archiving the change.

## Brownfield adoption

The existing application predates OpenSpec. Do not invent baseline requirements
that the code has not been checked against. Capture the desired behavior in a
change, reconcile it with the implementation, and archive the completed change;
archiving grows `openspec/specs/` into the source of truth one capability at a
time.

The target product support policy is Revit 2025-2027. Legacy build configurations
can be removed through a dedicated change rather than silently encoded as current
supported behavior.
