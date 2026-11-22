# 0004. Write the policy as precedents with reasons, not as rules

- Status: Proposed
- Date: 2026-11-22
- Related: [0003](0003-classifier-vs-single-policy.md), [0008](0008-eval-corpus-and-judge.md)

## Context

The policy tells the reviewer what counts as a vulnerability in our systems: trust boundaries, accepted risks, and decisions the security team has already made. Two styles are possible:

- A rule: "Do not report SSRF in the reporting service."
- A precedent: "The reporting service makes outbound requests only through `OutboundClient`, which pins requests to the hosts in `Outbound:AllowedHosts`. Requests built from user input are acceptable when they go through it, and a request that bypasses it is a finding."

The upstream action combined regex hard exclusions with a list of precedents. Its regexes drop findings by keyword. For example, any finding whose text matches "out of bounds" is dropped unless the file extension is `.c`, `.cc`, `.cpp` or `.h`, whatever the finding actually says.

## Considered options

### Option A: Rules and regex exclusions

- Pro: Deterministic and cheap.
- Con: A rule carries no reasoning, so the model cannot tell when a new case falls outside the rule's intent. Regexes match words, not meaning, and drop real findings silently.

### Option B: Precedents, each with a statement, a reason and an example

- Pro: The reason lets the reviewer generalize to cases the author did not foresee, and tells a later editor why the precedent exists.
- Pro: The example anchors the statement to code from the target stack.
- Con: Longer than rules, and the outcome is probabilistic, so it has to be measured by the eval ([0008](0008-eval-corpus-and-judge.md)).

## Decision

Option B, with no regex exclusions. Each precedent in `policy/` has an `id`, a `statement`, a `reason` and an `example`. `python -m sia_review validate-policy` fails if any of the four is missing or if an id is duplicated. Findings cite the precedent ids they relied on (`precedent_ids`), so a disputed finding leads back to the precedent that caused it.

Precedents describe our architecture, not individual eval cases. Writing a precedent that names a corpus case turns the eval into a memory test. Reviewers of policy changes reject such precedents ([policy authoring guide](../policy-authoring.md)).

## Consequences

- Policy changes go through pull requests owned by the security team. They are tested by rerunning the eval before merging.
- The starter policy describes the synthetic demo app. A company rollout replaces the trust boundaries and the examples, and keeps the structure.
