# 0002. Single policy with criteria as sections; no pre-review classifier

- Status: Proposed
- Date: 2026-11-22
- Related: [0001](0001-composite-action-vs-custom-service.md), [0003](0003-precedents-vs-rules.md), [0011](0011-policy-layering.md)

## Context

In the manual SIA process, the engineer picks the criteria that apply: Authorization, Authentication, Network, UI/frontend, Significant change, Data handling/PII, Secrets/crypto, Third-party dependencies, and Infrastructure as code. One proposal is to move that choice into an LLM classifier that runs before the review and labels the PR. The labels would then choose the instructions, the model, or whether the deep review runs at all.

A misrouted PR is a silent miss. If the classifier says "frontend only" and the change also weakens an authorization check, the review never sees the authorization precedents.

The upstream action takes one set of scan instructions and one set of filtering instructions per run. It has no notion of routing.

## Considered options

### Option A: LLM classifier as a gate

- Pro: Trivial PRs could skip the deep review.
- Con: Recall is capped by the classifier's recall, and the required check gains a second model call that can fail.

### Option B: Classifier that selects which policy sections to send

- Pro: Shorter instruction files.
- Con: The same misrouting problem for the left-out precedents. The saving is small, since the whole base policy is a few thousand words.

### Option C: Single policy; the reviewer labels each finding with its criterion

- Pro: Every PR gets every precedent, so there is no routing error.
- Pro: It fits the upstream inputs as they are.
- Con: A PR with no findings records nothing about which criteria it touched. The upstream output schema has no field for that.

## Decision

Option C. The rendered scan instructions list the nine criteria with their ids. They tell the reviewer to set each finding's `category` to the criterion id, and the PR summary groups findings by `category`.

A finding that fits none of the nine criteria uses `category: other`. Every `other` finding is a signal that a criterion or precedent is missing. No classifier is built. The eval reports how often a matched finding's `category` equals the case's expected criterion.

## Consequences

- The SIA record that the ticket's checkboxes used to hold is complete only for PRs with findings. For PRs without findings, it says only "reviewed, no findings".

## Open questions

- [decide: how a change that needs a security engineer's judgement reaches one when the reviewer reports nothing, for example a new authentication design or a new service. Candidates: CODEOWNERS on sensitive paths, requiring [security-team] review; an escalation flag from a criteria labeller that requests [security-team] review automatically; or a review the author requests.]

## Future opportunities

- **Criteria labeller, not a gate.** A cheap model call (Haiku or Sonnet) that labels every PR with the criteria touched, run in parallel with the review and never deciding whether the review runs. It fills the SIA-record gap above. Build it if auditors need per-PR criteria for PRs without findings.
- **Deterministic skip** for PRs that change only `*.md` or `docs/**`, using a path rule rather than a model. This needs production data on how common such PRs are and what they cost.
