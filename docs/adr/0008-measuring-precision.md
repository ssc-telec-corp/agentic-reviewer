# 0008. Measure precision from author feedback in production and from clean cases in the eval

- Status: Proposed
- Date: 2026-11-22
- Related: [0001](0001-composite-action-vs-custom-service.md), [0003](0003-precedents-vs-rules.md), [0005](0005-required-check-semantics.md), [0007](0007-eval-corpus-and-judge.md), [0009](0009-rollout-gates.md)

## Context

A corpus of historical vulnerabilities measures recall. It cannot measure precision, because every historical vulnerability in it is a true positive. The clean cases in the eval corpus ([0007](0007-eval-corpus-and-judge.md)) measure only false positives on a few simple changes. Precision decides whether engineers read the bot's comments at all. The reference design held developer-facing comments back until a hand-labelled precision of 70% held over two weeks. Its stated reason was that most engineers will read a comment if seven of ten are valid.

## Considered options

### Option A: Eval-only precision (clean cases plus unmatched extra findings)

- Pro: Automated and repeatable.
- Con: Clean synthetic cases are far fewer and simpler than real PRs. They catch gross regressions, not the false-positive rate engineers actually see.

### Option B: Production feedback from authors (reactions and replies on each inline comment)

- Pro: Measures precision on real PRs.
- Con: Authors may not react. Disagreement is not the same as being wrong, and a disputed finding needs a security engineer to settle it.

### Option C: Hand labelling during shadow mode

- Pro: The most accurate number before anyone sees a comment.
- Con: It costs security engineer time. It is a one-off unless repeated.

## Decision

Use all three, each for a different job:

- **Eval (Option A)** gates regressions. False positives on clean cases are capped in `eval/thresholds.yaml`. Findings on vulnerable cases that do not match the label are reported as "unmatched extras" for a human to label. They are not counted as false positives automatically, because they may be real.
- **Shadow mode (Option C)** gates turning on comments ([0009](0009-rollout-gates.md)). The security team labels every finding from the pilot repos for two weeks. Comments go live when precision is at least 70% over that period.
- **Author feedback (Option B)** monitors precision after launch. The upstream comment script (`addReactionsToReview()` in `scripts/comment-pr-findings.js`) adds a thumbs-up and a thumbs-down reaction to every inline comment it posts. This was seen in the 2026-11-22 test run. The reactions are a nudge: with the buttons already on the comment, giving feedback takes one click instead of a reply. They are a weak signal:
  - The bot casts the first vote on both, so every comment starts at one of each, and counting must subtract the bot's own reactions.
  - Most authors will not react, so the data is sparse.
  - A thumbs-down does not say whether the finding is false, true but not worth fixing, or rated at the wrong severity.
  - Nothing reads the reactions until the aggregation in the open questions below is built.

  Upstream has no setting to turn the reactions off. Removing them, or adding a footer that asks the author to reply when a finding is wrong, needs a change to the comment script, which depends on [0001](0001-composite-action-vs-custom-service.md). Hand labelling in shadow mode (Option C) and the bypass records ([0005](0005-required-check-semantics.md)) remain the reliable precision signals.
- **Precision of HIGH findings** is tracked separately, because only HIGH findings block a merge ([0005](0005-required-check-semantics.md)). Phase 2 measures it before blocking turns on. Once blocking is on, every security bypass is a HIGH finding labelled wrong by a security engineer. That is the most reliable precision signal the system has.

## Consequences

- The POC reports only Option A numbers. Shadow-mode labelling and feedback aggregation are not built.
- Collecting reactions across repos needs a scheduled job or the service in [0001](0001-composite-action-vs-custom-service.md) Option D.

## Open questions

- [decide: where feedback is aggregated (a scheduled job reading reactions through the GitHub API, or a data warehouse export), and who reviews disputed findings.]
