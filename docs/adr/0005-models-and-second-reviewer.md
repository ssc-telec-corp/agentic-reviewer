# 0005. Models: Opus reviewer with adjudication; a second Anthropic reviewer is measured, not deployed

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0007](0007-fail-open-vs-fail-closed.md), [0008](0008-eval-corpus-and-judge.md)

## Context

The reference design runs two reviewers from different vendors on every PR and posts the union of their findings, because different models miss different bugs. A third pass, the adjudicator, re-examines the candidates the reviewer talked itself out of. Our company uses only Anthropic models, so vendor diversity is not available. The diversity we can get comes from model size (Opus vs Sonnet), effort level, and prompt.

## Considered options

### Option A: One reviewer, no adjudication

- Pro: Cheapest and fastest.
- Con: Candidates the model dismisses as "pre-existing" or "low confidence" are lost. Those self-dismissals are exactly what adjudication is meant to catch.

### Option B: One reviewer (Opus) plus an adjudication pass over its dropped candidates

- Pro: Adjudication runs only when the reviewer dropped something, so it adds cost only on those PRs.
- Pro: The reviewer must report dropped candidates in structured form, which also explains misses in the eval ([0008](0008-eval-corpus-and-judge.md)).
- Con: Both passes share the same model's blind spots.

### Option C: Option B plus a second reviewer on a different Anthropic model (Sonnet), posting the union

- Pro: Some model diversity.
- Con: About twice the cost per PR, and more false positives, since the union adds both models' errors.
- Con: The benefit is unknown until the eval measures how often Sonnet catches what Opus misses.

## Decision

Option B in the action. The reviewer runs `claude-opus-5-5` at effort `high`. The adjudicator runs the same model, with the same policy and tools, only on dropped candidates. Model IDs are pinned in `action/action.yml` inputs.

The eval harness runs every case on both `claude-opus-5-5` and `claude-sonnet-5-5`. It reports per-model recall and union recall, which is the data needed to decide on Option C.

Failover: `claude --fallback-model claude-sonnet-5-5` handles overload of the primary model, and the reviewer retries a failed pass once ([0007](0007-fail-open-vs-fail-closed.md)).

## Consequences

- When the action falls back to Sonnet, the run's quality is the Sonnet number from the eval report, not the Opus number. The summary comment states which model produced the result.
- Changing the model ID is a policy-level change and requires an eval rerun ([0008](0008-eval-corpus-and-judge.md)).

## Open questions

- [decide: adopt Option C if union recall on the corpus exceeds Opus recall by at least [X] cases while union false positives stay within the threshold in [0008](0008-eval-corpus-and-judge.md). The POC numbers are in `docs/design.md` section 10. A corpus of 36 synthetic cases is too small to settle this; it needs real cases.]
- [decide: effort `high` vs `xhigh` for the primary reviewer. Not measured in the POC.]
