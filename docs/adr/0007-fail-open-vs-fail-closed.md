# 0007. Fail closed by default, with an org variable for break-glass fail-open

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0005](0005-models-and-second-reviewer.md), [0006](0006-required-check-semantics.md), [0010](0010-rollout-gates.md)

## Context

The review depends on the Anthropic API and GitHub Actions. The upstream action fails open without saying so. When Claude Code or the API errors, the scan step logs a warning, reports zero findings, and the job passes. When the filter's validation call fails, it posts unfiltered findings ([0002](0002-composite-action-vs-custom-service.md)). Without a wrapper check, an outage looks like a clean review.

With one vendor and no fallback model ([0005](0005-models-and-second-reviewer.md)), a full API outage stops every review in the org.

## Considered options

### Option A: Fail open (upstream behaviour)

- Pro: Outages never block merges.
- Con: Changes merge unassessed and nobody is told. That is the gap the SIA process exists to close. An oversized diff that makes the scan error also gets a pass.

### Option B: Fail closed with no override

- Pro: No change merges unreviewed.
- Con: An Anthropic outage halts every deploy in the org.

### Option C: Fail closed by default; security on-call flips an org variable to fail open during an incident

- Pro: Unreviewed merges happen only through a recorded human decision, and the run's job summary marks them for backfill.
- Con: Someone must remember to flip the variable back.

## Decision

Option C. After the upstream step, the wrapper's verify step classifies the run:

- **error**: `claudecode-results.json` is missing, empty, or contains an `error` key.
- **unfiltered**: `claudecode-error.log` contains `Claude API validation failed` or `Failed to initialize Claude client`.
- **completed**: neither of the above.

The workflow passes `fail-mode: ${{ vars.SIA_FAIL_MODE || 'closed' }}`. With `closed`, both error and unfiltered fail the check. With `open`, they pass with a warning annotation, and the job summary says "SIA review skipped: <reason>". `SIA_FAIL_MODE` is an organization variable, so changing it needs org admin rights and is recorded in the org audit log.

## Consequences

- During an outage, on-call sets `SIA_FAIL_MODE=open`. Afterwards they set it back and re-run the skipped checks. The [runbook](../runbook.md) has the commands.
- The verify step depends on upstream's file names and log strings. Bumping the upstream SHA must include a test run that exercises the error path.

## Future opportunities

- Retry with a fallback model before declaring an error ([0005](0005-models-and-second-reviewer.md)).
- Automatic revert of `SIA_FAIL_MODE` after [N] hours.
- [decide: who besides security on-call may flip the variable.]
