# 0002. Composite action with an in-house reviewer, not a wrapper and not a service

- Status: Proposed
- Date: 2026-11-22
- Related: [0003](0003-classifier-vs-single-policy.md), [0005](0005-models-and-second-reviewer.md), [0008](0008-eval-corpus-and-judge.md), [0012](0012-local-vs-ci-authentication.md)

## Context

The starting point was `anthropics/claude-code-security-review` (reviewed at commit `0c6a49f`), a composite GitHub Action. It runs Claude Code headless on a pull request diff to find candidates, removes candidates with regex hard exclusions, and then sends each remaining finding to the Messages API to filter false positives. Reading its source shows four properties that conflict with our requirements:

1. **It is bound to a GitHub pull request.** `github_action_audit.py` fetches the PR metadata and diff from the GitHub API using `GITHUB_TOKEN` and `PR_NUMBER`. The eval corpus ([0008](0008-eval-corpus-and-judge.md)) is a list of commits, so the eval harness cannot run the same reviewer without first opening a PR for every case.
2. **It requires an API key.** The filter stage calls the Messages API through the `anthropic` SDK with `ANTHROPIC_API_KEY`. Development is on a subscription login ([0012](0012-local-vs-ci-authentication.md)).
3. **By default it reviews only the first commit of a PR.** A cache marker disables later runs, and the comment script skips posting if any earlier bot security comment exists. A required check needs a result on every head commit.
4. **It discards dropped candidates.** Candidates removed by the filter appear only in the results file and are never re-examined. The adjudication pass ([0005](0005-models-and-second-reviewer.md)) needs them as structured input.

## Considered options

### Option A: Wrap the upstream action unchanged and pass our policy through its two instruction-file inputs

- Pro: No reviewer code to maintain.
- Con: Fails requirements 1 to 4 above. The eval would measure a different reviewer than the one in CI.

### Option B: Fork the upstream action

- Pro: Keeps its tests and comment script.
- Con: Requirements 1, 2 and 4 mean rewriting the orchestrator, the filter client and the prompt, which leaves little of the original. Rebasing on upstream changes becomes a manual merge.

### Option C: A composite action whose reviewer is a small in-house Python package that calls Claude Code headless

- Pro: The same `sia_review` package runs from the eval harness (input: a git tree and a diff) and from the action (input: a PR checkout). One code path is measured and deployed.
- Pro: Every model call goes through the `claude` CLI, so a subscription login, an OAuth token and an API key all work ([0012](0012-local-vs-ci-authentication.md)).
- Con: About 600 lines of Python that we own.

### Option D: A custom review service (webhook receiver plus job queue)

- Pro: Central retries, telemetry and cross-repo state such as fix tracking.
- Con: Needs hosting, a GitHub App and on-call for the service itself. None of that is needed to show whether review quality meets the bar.

## Decision

Option C. The action in `action/` runs `python -m sia_review review` and then `python -m sia_review post`. It keeps the upstream design ideas: a find pass followed by a skeptical pass, a JSON finding schema, precedents supplied as text, inline review comments with reaction buttons for feedback. It does not call or vendor upstream code. It drops the regex hard exclusions ([0004](0004-precedents-vs-rules.md)).

## Consequences

- Deliverable 1 changes from "wraps `claude-code-security-review`" to "replaces it with an action built on the same design."
- Without a service there is no central state. Telemetry is limited to job summaries and result artifacts, and fix tracking is not built ([0009](0009-measuring-precision.md)).
- Revisit Option D when org-wide telemetry or cross-PR state becomes a requirement, or when the number of required-check runs makes per-job setup time (installing Node and Python packages) a cost.
