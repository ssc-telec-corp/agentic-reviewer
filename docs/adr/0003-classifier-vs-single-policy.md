# 0003. Single policy with criteria as sections; no pre-review classifier

- Status: Proposed
- Date: 2026-11-22
- Related: [0004](0004-precedents-vs-rules.md), [0005](0005-models-and-second-reviewer.md), [0009](0009-measuring-precision.md)

## Context

In the manual SIA process, the engineer picks the criteria that apply: Authorization, Authentication, Network, UI/frontend, Significant change, Data handling/PII, Secrets/crypto, Third-party dependencies, and Infrastructure as code. One proposal moves that selection into an LLM classifier that runs before the review. The classifier would label the PR with criteria and use the labels to choose the instructions, the model, or whether the deep review runs at all.

Two facts shape the choice. First, a misrouted PR is a silent miss. If the classifier says "frontend only" and the change also weakens an authorization check, the deep review never sees the authorization precedents. Second, the review cost has to be measured before a cost argument for skipping it can be made. The POC measures it per case ([eval report](../../eval/reports/latest/report.md)).

## Considered options

### Option A: LLM classifier as a gate

- Pro: Cheap PRs (docs, copy changes) could skip the deep review.
- Pro: The prompt for each PR is shorter, since it carries only the selected criteria's precedents.
- Con: Recall is capped by classifier recall. Every classifier miss is a review miss, and the eval would have to measure two models instead of one.
- Con: It adds a second prompt, a second model call and a second failure mode to the required check.

### Option B: Classifier that only selects instructions, while every PR still gets the deep review

- Pro: The prompt is shorter than Option C's.
- Con: It has the same misrouting problem for the precedents that get left out. The token saving is small, because the whole policy is about 5,000 words.

### Option C: Single shared policy; the reviewer reports the criteria it touched

- Pro: One prompt and one model call carry every precedent, so there is no routing error.
- Pro: The reviewer returns `criteria_touched` with a one-line reason for each. That is the SIA record the ticket used to hold, at no extra cost.
- Con: Every PR pays for the full policy in its prompt, and prompt caching reduces only part of that.

## Decision

Option C. `policy/criteria/*.yaml` holds one section per criterion, and the reviewer prompt includes all of them. The review output's `criteria_touched` field records which criteria the change touched. The eval harness reports how often `criteria_touched` agrees with the corpus labels. That gives the measurement a classifier would have produced, without the gate.

Deliverable 4 (`classifier/`) is not built.

## Consequences

- The PR summary comment lists the criteria touched and why, which replaces the SIA ticket's criteria checkboxes.
- Criterion labels have no effect on whether a finding is reported. A finding outside the labelled criteria is still posted.

## Open questions

- [decide: whether to skip the review for PRs that change only non-code paths (for example `*.md` or `docs/**`), using a deterministic path rule rather than an LLM. Needs production data on the share of such PRs and their review cost.]
