# 0002. Single policy; the review classifies every PR itself, with no gate and no extra comment

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-reviewer-core.md), [0003](0003-precedents-vs-rules.md), [0005](0005-required-check-semantics.md), [0011](0011-policy-layering.md)

**In short:** every PR gets the full review, with all SIA criteria in one policy. The review also records which criteria the PR touched, for every PR, including PRs with no findings. That classification goes into the job summary and the results artifact, and is never posted as a PR comment. A PR with no findings gets no comment at all.

## Context

In the manual SIA process, the engineer picks the criteria that apply: Authorization, Authentication, Network, UI/frontend, Significant change, Data handling/PII, Secrets/crypto, Third-party dependencies, and Infrastructure as code. One proposal is to move that choice into an LLM classifier that runs before the review and labels the PR. The labels would then choose the instructions, the model, or whether the deep review runs at all.

A misrouted PR is a silent miss. If the classifier says "frontend only" and the change also weakens an authorization check, the review never sees the authorization precedents.

The automated review supplements the human code review that every PR already requires. It does not replace that review, and it does not route PRs to the security team. A PR with no findings is the expected, good outcome, and needs no attention.

The upstream action takes one set of scan instructions per run and has no notion of routing. It passes the model's `analysis_summary` object through to its results unchanged, so extra fields placed there reach the wrapper.

## Considered options

### Option A: LLM classifier as a gate

- Pro: Trivial PRs could skip the deep review.
- Con: Recall is capped by the classifier's recall, and the required check gains a second model call that can fail.

### Option B: Classifier that selects which policy sections to send

- Pro: Shorter instruction files.
- Con: The same misrouting problem for the left-out precedents, for a small saving.

### Option C: Single policy; only findings carry a criterion

- Pro: Every PR gets every criterion, so there is no routing error.
- Con: A PR with no findings records nothing about which criteria it touched, so the SIA record is incomplete.

### Option D: Option C, plus the review classifies every PR and records it without posting

- Pro: A complete SIA record for every PR, with no routing error and no gate.
- Pro: No extra model call. The classification is part of the same review.
- Pro: No noise on clean PRs, because the classification is recorded, not commented.
- Con: The classification is only as good as the reviewer's reading of the change. The eval measures its agreement with the expected criteria.

## Decision

Option D.

- The scan instructions list the nine criteria with their ids. Each finding's `category` is set to a criterion id, or to `other` when none fits. Every `other` finding is a signal that a criterion or precedent is missing.
- The review also returns `criteria_touched`: the criteria the PR touches, each with a one-line reason, for every PR. With the upstream code ([0001](0001-reviewer-core.md) Option C), the scan instructions ask for it inside `analysis_summary`, which upstream passes through. With a headless reviewer (Option D), it is a field of the output schema.
- The wrapper writes `criteria_touched` to the job summary and the results artifact. It is never posted as a PR comment. When a review has no findings, the wrapper posts nothing on the PR ([0005](0005-required-check-semantics.md)).
- No separate classifier is built. The eval reports how often `criteria_touched` agrees with each case's expected criteria, and how often a matched finding's `category` equals the expected criterion.

## Consequences

- The SIA record, which used to be the ticket's checkboxes, exists for every PR, in the job summary and artifact rather than in the PR conversation.
- Engineers see the reviewer only when it has something to say.
- Escalation to a security engineer is not part of this system. A change that needs one goes through the existing review process.

## Future opportunities

- **Deterministic skip** for PRs that change only `*.md` or `docs/**`, using a path rule rather than a model. This needs production data on how common such PRs are and what they cost.
- **Exporting `criteria_touched`** from artifacts to a central store, if auditors need to query the SIA record across repositories.
