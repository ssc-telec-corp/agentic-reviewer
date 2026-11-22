# 0010. Phased rollout with measured exit criteria

- Status: Proposed
- Date: 2026-11-22
- Related: [0006](0006-required-check-semantics.md), [0007](0007-fail-open-vs-fail-closed.md), [0009](0009-measuring-precision.md)

## Context

Turning on an org-wide required check affects every engineer at once. A noisy reviewer trains people to ignore it. A broken workflow blocks every merge. The manual SIA process also has to keep running until the automated review is trusted.

## Considered options

### Option A: Big-bang: required org-wide from day one

- Pro: Fastest.
- Con: The first false positives and outages land on everyone. There is no baseline to compare against.

### Option B: Phases gated on measured criteria

- Pro: Each phase produces the data the next phase needs.
- Con: Slower. The manual SIA runs in parallel during the early phases.

## Decision

Option B. The action input `mode` (`shadow` or `comment`) and the org variable `SIA_FAIL_MODE` ([0007](0007-fail-open-vs-fail-closed.md)) are the controls. Each phase has an exit criterion:

| Phase | Scope | Settings | Exit criterion |
|---|---|---|---|
| 0. Baseline | Eval corpus only | n/a | Corpus contains at least [N] real cases; gate model meets `eval/thresholds.yaml` |
| 1. Shadow | [pilot repos], ruleset in evaluate mode | `mode: shadow`, `SIA_FAIL_MODE=open` | 2 weeks of findings labelled; precision at least 70% ([0009](0009-measuring-precision.md)) |
| 2. Comments | [pilot repos], ruleset active | `mode: comment`, `SIA_FAIL_MODE=open` | 2 more weeks at least 70% precision from author feedback; no unresolved runbook incidents |
| 3. Org-wide | All repos, ruleset active | `mode: comment`, `SIA_FAIL_MODE=open` | Failure rate of the check below [X]% of runs over 2 weeks; manual SIA retired |
| 4. Fail closed | All repos | `SIA_FAIL_MODE=closed` | n/a (steady state) |

In shadow mode the action writes findings to the job summary and the result artifact, and posts nothing on the PR. The manual SIA keeps running until the exit of phase 3.

## Consequences

- Phase 1 uses the ruleset's evaluate mode, so the check reports results without blocking. Whether evaluate mode is available for required workflows on our GitHub plan is Unknown, and has to be checked against the GitHub documentation for [GitHub plan].
- Phases 1 to 3 run fail-open, and the code default (closed) applies only from phase 4.

## Open questions

- [decide: the pilot repos, N (minimum number of real corpus cases), and X (maximum check failure rate).]
