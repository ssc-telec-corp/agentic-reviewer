# 0006. The required check means "review completed", not "no findings"

- Status: Proposed
- Date: 2026-11-22
- Related: [0007](0007-fail-open-vs-fail-closed.md), [0009](0009-measuring-precision.md), [0010](0010-rollout-gates.md)

## Context

An org ruleset makes the `sia-review` workflow a required status check on every PR to protected branches. We have to decide what makes that check pass. The manual SIA it replaces never blocked a merge on findings directly: the security team commented, and the engineer responded. The reference design considered blocking merges on open findings and rejected it as too much friction. It tracked fix rate instead, and improved fix rate by tightening the comment format.

A required check also needs a result on every head commit. A run that is skipped after the first push leaves the check pending, and the PR cannot merge.

## Considered options

### Option A: Pass only when there are no findings

- Pro: Findings cannot be ignored.
- Con: Every false positive blocks a merge until someone with bypass rights intervenes. At the 70% precision bar ([0009](0009-measuring-precision.md)), about 3 in 10 blocked PRs would be blocked wrongly.

### Option B: Pass when the review completed and its results were posted; findings are comments

- Pro: The engineer stays in control, and false positives cost a reply, not a blocked merge.
- Pro: The check still guarantees what the manual process did, that every change was assessed.
- Con: An engineer can merge with an unaddressed finding. That has to be measured (fix rate), since nothing prevents it.

### Option C: Option B, but fail on findings of severity `high`

- Pro: Blocks the worst cases.
- Con: It needs severity calibration that the POC does not measure, and it creates an incentive to argue severity.

## Decision

Option B. The check runs on `opened`, `synchronize`, `reopened` and `ready_for_review`, with one concurrency group per PR so a new push cancels the previous run. The check:

- passes when the reviewer completed and the post step succeeded, whatever the findings;
- fails when the reviewer could not complete, unless fail-open mode is set ([0007](0007-fail-open-vs-fail-closed.md));
- fails when the PR comes from a fork ([0011](0011-fork-prs-and-untrusted-content.md)).

Each run re-reviews the full PR diff. The post step updates one summary comment in place, and posts inline comments only for findings whose fingerprint (criterion, file and title) has not already been posted on that PR.

The action has a `fail-on-severity` input (default `none`) so that Option C can be turned on without a code change.

## Consequences

- Fix rate (findings addressed before merge) becomes the outcome metric. The POC does not build fix tracking ([0009](0009-measuring-precision.md)).
- Re-reviewing on every push costs a full review per push. The cost per review is in the eval report.

## Open questions

- [decide: whether to set `fail-on-severity: high` after phase 3 of the rollout ([0010](0010-rollout-gates.md)), based on the measured precision of `high` findings.]
