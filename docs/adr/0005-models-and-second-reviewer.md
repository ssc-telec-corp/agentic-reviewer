# 0005. Models: one Anthropic model per run; second reviewer and adjudication are future options

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0007](0007-fail-open-vs-fail-closed.md), [0008](0008-eval-corpus-and-judge.md)

## Context

The reference design runs two reviewers from different vendors on every PR and posts the union, because different models miss different bugs. It also adds an adjudicator that re-examines the candidates the reviewer dropped. Our company uses only Anthropic models.

In the upstream action, a single input, `claude-model`, sets the model for both the Claude Code find stage and the Messages API filter. Its default is `claude-opus-4-1-20250805`. There is no fallback model and no adjudication.

## Considered options

### Option A: One model for find and filter, set through `claude-model`

- Pro: Supported by the upstream action as it is.
- Con: Find and filter share the same model's blind spots.

### Option B: Run the upstream action twice per PR with different models (Opus and Sonnet)

- Pro: Some model diversity without a fork.
- Con: Twice the cost. The two runs post separately, and the upstream duplicate check would suppress the second run's inline comments.

### Option C: Add an adjudication step after the upstream action over `excluded_findings_details`

- Pro: Recovers true findings that the filter talked itself out of. The reference design reports about a 30% relative recall gain from adjudication on known-bad commits.
- Con: Needs a model call outside the upstream action, which means code we own.

## Decision

Option A, with `claude-model: claude-opus-5-5`. The eval harness runs every corpus case on both `claude-opus-5-5` and `claude-sonnet-5-5`. It reports each model's recall and false positives, and the recall of their union. Those numbers are what Options B and C need before anyone builds them.

## Consequences

- There is no automatic fallback model. When Opus is unavailable, the scan errors and the fail-mode rule in [0007](0007-fail-open-vs-fail-closed.md) applies.
- Changing `claude-model` requires an eval run ([0008](0008-eval-corpus-and-judge.md)).

## Future opportunities

- **Adjudication (Option C).** Build it first if the eval shows misses classified as "dropped by filter".
- **Second reviewer (Option B).** Adopt it if union recall beats Opus recall by at least [X] cases while union false positives stay within the threshold. A 37-case synthetic corpus is too small to decide this, so it needs real cases.
- **Fallback model.** Retry the upstream step with `claude-sonnet-5-5` when the first attempt errors. This is a wrapper step, not a fork.
