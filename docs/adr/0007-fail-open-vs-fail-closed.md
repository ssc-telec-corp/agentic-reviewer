# 0007. Fail closed by default, with an org variable for break-glass fail-open

- Status: Proposed
- Date: 2026-11-22
- Related: [0005](0005-models-and-second-reviewer.md), [0006](0006-required-check-semantics.md), [0010](0010-rollout-gates.md)

## Context

The review depends on the Anthropic API and on GitHub Actions. When the API is unavailable, the required check cannot complete. The reference design treated a missing review as a reason not to merge, and used failover across vendors to keep outages rare. With a single vendor, failover is limited to falling back to another model and retrying ([0005](0005-models-and-second-reviewer.md)). A full API outage therefore blocks every merge in the org.

## Considered options

### Option A: Fail open (the check passes when the review cannot run)

- Pro: Outages never block merges.
- Con: Changes merge without assessment, silently. That reproduces the gap the SIA process exists to close. An attacker who can degrade the API call (for example with an oversized diff) also gets a skip.

### Option B: Fail closed with no override

- Pro: No change merges unreviewed.
- Con: An Anthropic outage halts every deploy in the org, including fixes for the outage's own effects.

### Option C: Fail closed by default; security on-call flips an org variable to fail open during an incident, and skipped PRs are marked for backfill

- Pro: Unreviewed merges happen only by a recorded human decision.
- Pro: Skipped PRs carry a summary comment saying that the SIA review was skipped because of an outage, so they can be found and reviewed later.
- Con: Someone has to remember to flip the variable back. The runbook covers this.

## Decision

Option C.

- The reviewer retries each failed model call once. Claude Code's `--fallback-model` moves to `claude-sonnet-5-5` when the primary model is overloaded. A call fails when the CLI exits non-zero, when the result has `is_error: true`, or when the structured output is missing.
- When every attempt fails, `sia_review review` exits with code 3 (infrastructure failure), which is distinct from 0 (completed).
- The workflow passes `fail-mode: ${{ vars.SIA_FAIL_MODE || 'closed' }}` to the action. With `closed`, exit 3 fails the check. With `open`, the action posts the skip comment, writes a warning annotation, and passes.
- `SIA_FAIL_MODE` is an organization variable. Changing it needs org admin rights, and the org audit log records who changed it and when.

## Consequences

- During an outage, on-call sets `SIA_FAIL_MODE=open`. After the outage they set it back and re-run the skipped checks. The [runbook](../runbook.md) has the commands.
- A PR whose diff is too large for the model also exits 3 and fails closed. That is deliberate, and the runbook gives the bypass path.

## Open questions

- [decide: who besides security on-call may flip `SIA_FAIL_MODE`, and the maximum duration before it is reverted automatically. The POC does not build an automatic revert.]
