# 0008. Measure precision from author feedback in production and from clean cases in the eval

- Status: Proposed
- Date: 2026-11-22
- Related: [0005](0005-required-check-semantics.md), [0007](0007-eval-corpus-and-judge.md), [0009](0009-rollout-gates.md)

## Context

A corpus of historical vulnerabilities measures recall. It cannot measure precision, because every case in it is a true positive. Precision decides whether engineers read the bot's comments at all. The reference design held developer-facing comments back until a hand-labelled precision of 70% held over two weeks. Its stated reason was that most engineers will read a comment if seven of ten are valid.

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
- **Author feedback (Option B)** monitors precision after launch. Every inline comment gets thumbs-up and thumbs-down reactions added by the bot as an invitation to react, and the footer asks the author to reply if the finding is wrong.
- **Precision of HIGH findings** is tracked separately, because only HIGH findings block a merge ([0005](0005-required-check-semantics.md)). Phase 2 measures it before blocking turns on. Once blocking is on, every security bypass is a HIGH finding labelled wrong by a security engineer. That is the most reliable precision signal the system has.

## Consequences

- The POC reports only Option A numbers. Shadow-mode labelling and feedback aggregation are not built.
- Collecting reactions across repos needs a scheduled job or the service in [0001](0001-composite-action-vs-custom-service.md) Option D.

## Open questions

- [decide: where feedback is aggregated (a scheduled job reading reactions through the GitHub API, or a data warehouse export), and who reviews disputed findings.]
