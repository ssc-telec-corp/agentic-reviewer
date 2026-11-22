# 0009. Phased rollout with measured exit criteria

- Status: Proposed
- Date: 2026-11-22
- Related: [0005](0005-required-check-semantics.md), [0006](0006-fail-open-vs-fail-closed.md), [0008](0008-measuring-precision.md), [0012](0012-versioned-releases.md)

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

Option B. Three organization variables are the controls, so phases change without editing workflows:

- `ASR_COMMENT_PR` (`true` or `false`) switches the wrapper's posting step ([0005](0005-required-check-semantics.md)). `false` is shadow mode. Upstream's own commenting is always off.
- `ASR_FAIL_ON_SEVERITY` (`none` or `high`) controls whether HIGH findings block the merge ([0005](0005-required-check-semantics.md)).
- `ASR_FAIL_MODE` (`open` or `closed`) controls whether the check fails when the review could not run ([0006](0006-fail-open-vs-fail-closed.md)).

A repository variable with the same name overrides the organization variable, which is how a phase applies to the pilot repos only.

| Phase | Scope | Settings | Exit criterion |
|---|---|---|---|
| 0. Baseline | Eval corpus only | n/a | Corpus contains at least [N] real cases; gate model meets `eval/thresholds.yaml` |
| 1. Shadow | [pilot repos], ruleset in evaluate mode | comments off, blocking `none`, fail mode `open` | 2 weeks of findings labelled; precision at least 70% ([0008](0008-measuring-precision.md)) |
| 2. Comments | [pilot repos], ruleset active | comments on, blocking `none`, fail mode `open` | 2 more weeks at least 70% precision from author feedback; precision of HIGH findings at least [X]% |
| 3. Blocking on pilots | [pilot repos] | comments on, blocking `high`, fail mode `open` | 2 weeks with the bypass process running within [SLA]; every bypass recorded |
| 4. Org-wide | All repos, ruleset active | comments on, blocking `high`, fail mode `open` | Failure rate of the check, excluding HIGH findings, below [Y]% of runs over 2 weeks; manual SIA retired |
| 5. Fail closed | All repos | fail mode `closed` | n/a (steady state) |

In shadow mode the wrapper writes findings to the job summary, upstream uploads its result artifact, and nothing is posted on the PR. The manual SIA keeps running until the exit of phase 4.

## Consequences

- Phase 1 uses the ruleset's evaluate mode, so the check reports results without blocking. Whether evaluate mode is available for required workflows on our GitHub plan is Unknown, and has to be checked against the GitHub documentation for [GitHub plan].
- Phases 1 to 4 run fail-open, and the code default (closed) applies only from phase 5.
- HIGH findings block a merge only from phase 3, after phase 2 has measured how often they are right.
- Organization variables are not accessible to private repositories on GitHub Free, according to GitHub's documentation. The controls therefore assume [GitHub plan] is a paid plan.
- A repository variable overrides an organization variable with the same name. That is how pilot repos get their own settings, but it also means any repository admin can set `ASR_FAIL_ON_SEVERITY=none` or `ASR_FAIL_MODE=open` on their own repository and switch off blocking for their own PRs. The wrapper writes the values it used to every run's job summary, so an override is visible after the fact, but nothing prevents it.

## Future opportunities

- If repository-level overrides are seen in practice, move the controls to repository variables of the central `adaptive-security-reviewer` repository, which only [security-team] administers. The wrapper would read them through the GitHub API with a security-owned GitHub App token, and ignore variables in the reviewed repository.

## Open questions

- [decide: how phase 4's exit is measured. Under fail-open, a review that could not run passes the check, so check failures undercount. Count the verify step's classifications instead ([0006](0006-fail-open-vs-fail-closed.md)).]
- [decide: who reviews PRs that merged unreviewed while fail-open was in effect, and when.]
- [decide: where shadow-mode findings are kept for two weeks of labelling. Upstream's result artifacts are deleted after 7 days.]

- [decide: the pilot repos, N (minimum number of real corpus cases), X (minimum HIGH precision before blocking), and Y (maximum check failure rate).]
- [decide: who checks for repository-level `ASR_*` overrides, for example in the periodic bypass review ([0005](0005-required-check-semantics.md)), and how often.]
