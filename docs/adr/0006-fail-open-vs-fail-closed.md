# 0006. Fail closed by default, with an org variable for break-glass fail-open

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-reviewer-core.md), [0004](0004-models-and-second-reviewer.md), [0005](0005-required-check-semantics.md), [0009](0009-rollout-gates.md)

## Context

The review depends on the Anthropic API and GitHub Actions. The upstream action fails open without saying so. When Claude Code or the API errors, the scan step logs a warning, reports zero findings, and the job passes. When the filter's validation call fails, it posts unfiltered findings ([0001](0001-reviewer-core.md)). Without a wrapper check, an outage looks like a clean review.

With one vendor and no fallback model ([0004](0004-models-and-second-reviewer.md)), a full API outage stops every review in the org.

## Considered options

### Option A: Fail open (upstream behaviour)

- Pro: Outages never block merges.
- Con: Changes merge unassessed and nobody is told. That is the gap the SIA process exists to close. An oversized diff that makes the scan error also gets a pass.

### Option B: Fail closed with no override

- Pro: No change merges unreviewed.
- Con: An Anthropic outage halts every deploy in the org.

### Option C: Fail closed by default; the rotation engineer flips an org variable to fail open during an incident

- Pro: Unreviewed merges happen only through a recorded human decision, and the run's job summary marks them for backfill.
- Con: Someone must remember to flip the variable back.

## Decision

Option C. After the upstream step, the wrapper's verify step classifies the run:

- **error**: `claudecode-results.json` is missing, empty, or contains an `error` key.
- **unfiltered**: `claudecode-error.log` contains `Claude API validation failed` or `Failed to initialize Claude client`.
- **completed**: neither of the above.

The workflow passes `fail-mode: ${{ vars.ASR_FAIL_MODE || 'closed' }}`. With `closed`, both error and unfiltered fail the check. With `open`, they pass with a warning annotation, and the job summary says "SIA review skipped: <reason>". `ASR_FAIL_MODE` is an organization variable. Changing it needs org admin rights, and the org audit log records the change. A repository admin can still override it for their own repository ([0009](0009-rollout-gates.md)).

## Consequences

- During an outage, the rotation engineer sets `ASR_FAIL_MODE=open`. Afterwards they set it back and re-run the skipped checks. The [runbook](../runbook.md) has the commands.
- A repository admin can override `ASR_FAIL_MODE` for their own repository with a repository variable. See [0009](0009-rollout-gates.md) for this risk.
- The verify step depends on upstream's file names and log strings. Bumping the upstream SHA must include a test run that exercises the error path.

## Known gaps

The classification above misses three ways the upstream code fails silently. They are covered by whichever reviewer-core option is chosen ([0001](0001-reviewer-core.md)):
- Output that is not the expected JSON becomes an empty, successful review (problem 2 in 0001). Check that `analysis_summary.review_completed` is `true`.
- A finding whose filter call failed is kept unfiltered (problem 3). Check for `Claude API failed` in `_filter_metadata.justification`.
- If the upstream step exits with an error, for example when posting comments fails, later steps are skipped. The verify and summary steps need `if: always()`.

## Future opportunities

- Retry with a fallback model before declaring an error ([0004](0004-models-and-second-reviewer.md)).
- Automatic revert of `ASR_FAIL_MODE` after [N] hours.
- [decide: who besides the rotation engineer may flip the variable.]
