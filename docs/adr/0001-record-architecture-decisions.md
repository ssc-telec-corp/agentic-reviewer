# 0001. Record architecture decisions

- Status: Proposed
- Date: 2026-11-22
- Related: all ADRs in this directory

## Context

The automated Security Impact Assessment (SIA) review replaces a manual process that the security team owns. Several choices in it depart from the reference design it was modelled on (a two-vendor reviewer run as a custom service), and the reasons for those departures need to survive staff changes.

## Considered options

### Option A: Michael Nygard ADRs in the repository

- Pro: Versioned with the code and policy they describe; changes go through the same pull request review.
- Pro: One decision per file keeps each record short enough to read before changing the related code.
- Con: Records can go stale if nobody updates the status when the code changes.

### Option B: A single design document only

- Pro: One place to read.
- Con: Reasons get edited away when the design changes; the history of why an option was rejected is lost.

### Option C: Wiki pages

- Pro: Easy to edit.
- Con: Not reviewed with code changes; drifts from what is deployed.

## Decision

Use Option A. Each ADR lives in `docs/adr/NNNN-short-title.md`, uses the template in `template.md` (Nygard sections plus "Considered options" and "Open questions"), covers one decision, and fits on about one page. `docs/adr/README.md` lists every ADR with its status. `docs/design.md` states what is built and links to ADRs for why.

All ADRs start as Proposed. The security team lead moves an ADR to Accepted or Rejected [decide: owner, [security-lead]]. A changed decision gets a new ADR that supersedes the old one; the old one is not rewritten.

## Consequences

- A pull request that changes a decision recorded here must update or supersede the ADR in the same pull request.
- Undecided points are written as `[decide: ...]` in the relevant ADR, so they are visible rather than silently defaulted.
