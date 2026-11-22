# 0005. One reviewer model, Claude Opus 5.5; a second model is measured before it is used

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0007](0007-fail-open-vs-fail-closed.md), [0008](0008-eval-corpus-and-judge.md)

**In short:** every PR is reviewed by one model, `claude-opus-5-5`. We do not add a second reviewer model yet. Instead, the eval runs every test case on both Opus and Sonnet, and the results decide whether a second reviewer is worth its cost.

## Context

The reference design (Figma's) has every PR reviewed by two AI products from different vendors, Claude and OpenAI's Codex, and posts what either one finds. The reason is that different models miss different bugs, so two reviewers together catch more than either one alone.

Our company uses only Anthropic models, so a second vendor is not an option. The closest substitute is a second Anthropic model: Sonnet instead of Opus, which is smaller, cheaper and differently tuned. Whether Sonnet catches bugs that Opus misses on our code is Unknown.

The `claude-code-security-review` action has one setting for this, `claude-model`. It sets the model for both of the action's steps: the step that finds possible vulnerabilities, and the step that filters out false ones. Its default is the outdated `claude-opus-4-1-20250805`.

## Considered options

### Option A: One model, Opus 5.5

- Pro: The action supports it as it is; it is one setting.
- Pro: Opus is the strongest Anthropic model for the job, so it has the best chance of finding real bugs.
- Con: Every bug that Opus misses is missed. There is no second opinion.

### Option B: Two models, run the action twice per PR (Opus and Sonnet) and post what either finds

- Pro: The closest we can get to the two-vendor setup.
- Con: Each PR costs about twice as much.
- Con: Wrong findings from both models get posted, so engineers see more noise.
- Con: The action posts no inline comments if the PR already has any from the bot, so the second run's findings would appear only in the job summary.

### Option C: Sonnet only

- Pro: Cheapest.
- Con: Likely to miss more than Opus, though by how much is Unknown until the eval measures it.

## Decision

Option A. The wrapper sets `claude-model: claude-opus-5-5`.

To decide on Option B with data instead of opinion, the eval runs every test case on both `claude-opus-5-5` and `claude-sonnet-5-5`. For each model, it reports how many known vulnerabilities the model found and how many false findings it raised on known-clean changes. It also reports how many vulnerabilities at least one of the two found.

## Consequences

- If Opus is unavailable, the review fails, and the outage rules in [0007](0007-fail-open-vs-fail-closed.md) decide whether the check blocks the merge. There is no automatic switch to another model.
- Changing the model is a behaviour change: it needs an eval run and a new minor release ([0014](0014-versioned-releases.md)).

## Open questions

- [decide: adopt Option B if the two models together find at least [X] more of the known vulnerabilities than Opus alone, while false findings on clean changes stay within the threshold in `eval/thresholds.yaml`. The synthetic test set is too small to decide this, so it needs real cases ([0008](0008-eval-corpus-and-judge.md)).]

## Future opportunities

- **Second check of discarded findings ("adjudicator").** The action's filter discards some possible vulnerabilities, and lists them in its output under `excluded_findings_details`. A wrapper step after the action could ask the model to look at those again. Figma reports that this raised the share of known bugs found by about 30% (relative). Build it if the eval shows misses where the right finding was found and then discarded.
- **Fallback model during outages.** When the Opus run errors, the wrapper could retry the action once with Sonnet instead of failing the check. Then the run's quality is Sonnet's eval number, which the job summary would state.
