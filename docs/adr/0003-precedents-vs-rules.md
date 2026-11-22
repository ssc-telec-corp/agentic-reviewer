# 0003. Write the policy as precedents with reasons, not as rules

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-composite-action-vs-custom-service.md), [0002](0002-classifier-vs-single-policy.md), [0007](0007-eval-corpus-and-judge.md), [0008](0008-measuring-precision.md), [0011](0011-policy-layering.md)

## Context

The policy tells the reviewer what counts as a vulnerability in our systems: trust boundaries, accepted risks, and decisions the security team has already made. Two styles are possible:

- A rule: "Do not report SSRF in the reporting service."
- A precedent: "The reporting service makes outbound requests only through its allowlisted client. Requests built from user input are acceptable when they go through it, and a request that bypasses it is a finding."

The upstream action already uses precedents in its default filtering instructions: 17 of them, most with a sentence of reasoning. On top of those it applies regex hard exclusions in `findings_filter.py`. The regexes match keywords. For example, any finding that mentions "out of bounds" is dropped unless the file ends in `.c`, `.cc`, `.cpp` or `.h`, whatever the finding says. They cannot be turned off through an action input.

## Considered options

### Option A: Rules

- Pro: Short, and they look deterministic.
- Con: A rule carries no reasoning, so the model cannot tell when a new case falls outside the rule's intent.

### Option B: Precedents with a statement and a reason, plus an optional example

- Pro: The reason lets the reviewer generalize, and tells a later editor why the precedent exists.
- Pro: Same style as the upstream defaults, so the combined filter text reads as one document.
- Con: Longer than rules, and the outcome is probabilistic, so it has to be measured ([0007](0007-eval-corpus-and-judge.md)).

## Decision

Option B. Every precedent has an `id`, a `statement` and a `reason`, and may have an `example`. The validator fails on a missing statement or reason, or on a duplicate id. The example is optional, to keep precedents cheap to maintain.

Precedents describe architecture and recurring decisions, not individual eval cases. A precedent that names a corpus case turns the eval into a memory test, and reviewers of policy changes reject it.

Our precedents add to upstream's defaults and never repeat or contradict them, because upstream's defaults come first and win ([0011](0011-policy-layering.md)). Before a precedent is added, its author checks it against upstream's default text. For now no precedents are loaded, and the review runs on upstream's defaults alone ([0011](0011-policy-layering.md)).

The upstream regex exclusions stay active. Turning them off would require a fork ([0001](0001-composite-action-vs-custom-service.md)).

## Consequences

- A real finding whose text happens to match an exclusion regex is dropped before the policy is consulted. Such drops appear in `excluded_findings_details` with `filter_stage: hard_rules`. The eval reports them as their own miss category, so their effect is measured.

## Future opportunities

- If the eval shows real misses with `filter_stage: hard_rules`, propose an upstream input that disables or narrows the hard exclusions.
- Feed author feedback from disputed findings ([0008](0008-measuring-precision.md)) into new precedents, with a security-team review of each one.
