# 0006. The required check means "review completed", not "no findings"

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0007](0007-fail-open-vs-fail-closed.md), [0009](0009-measuring-precision.md), [0010](0010-rollout-gates.md)

## Context

An org ruleset makes the `sia-review` workflow a required status check on every PR to protected branches. The manual SIA it replaces never blocked a merge on findings directly: the security team commented and the engineer responded. The reference design considered blocking merges on open findings and rejected it as too much friction. It tracked fix rate instead.

A required check needs a result on every head commit. Upstream's default `run-every-commit: false` skips every run after the first on a PR. The skipped run still passes, so later commits would merge unreviewed.

## Considered options

### Option A: Fail the check when there are findings

- Pro: Findings cannot be ignored.
- Con: Every false positive blocks a merge until someone with bypass rights steps in. At the 70% precision bar ([0009](0009-measuring-precision.md)), about 3 in 10 blocks would be wrong.

### Option B: Pass when the review completed; findings are comments

- Pro: Engineers stay in control. The check still guarantees what the manual process did, that every change was assessed.
- Con: An engineer can merge with an unaddressed finding. That has to be measured through fix rate.

### Option C: Option B, but fail on findings of severity HIGH

- Pro: Blocks the worst cases.
- Con: Needs severity calibration that the POC does not measure.

## Decision

Option B. The workflow runs on `opened`, `synchronize`, `reopened` and `ready_for_review`, with one concurrency group per PR so a new push cancels the previous run. The wrapper passes `run-every-commit: true`. The check:

- passes when the upstream scan completed without error and the filter ran;
- fails when the scan errored or the filter disabled itself, unless fail-open mode is set ([0007](0007-fail-open-vs-fail-closed.md));
- fails for fork PRs ([0011](0011-fork-prs-and-untrusted-content.md)).

The upstream comment script posts inline comments only if no earlier bot security comment exists on the PR. To keep findings from later pushes visible, the wrapper writes all kept findings to the job summary on every run.

## Consequences

- The PR conversation can lag behind the job summary after the first push. The [engineer FAQ](../engineer-faq.md) explains where to look.
- Each push is a full review, at the per-review cost shown in the eval report.

## Future opportunities

- An input `fail-on-severity: high` (Option C), once the measured precision of HIGH findings supports it.
- Fingerprint-based posting of new findings on later pushes, either as a wrapper step or as an upstream contribution.
- Fix-rate tracking ([0009](0009-measuring-precision.md)).
