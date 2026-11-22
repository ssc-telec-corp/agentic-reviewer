# 0002. Wrap `claude-code-security-review` in a composite action

- Status: Proposed
- Date: 2026-11-22
- Related: [0005](0005-models-and-second-reviewer.md), [0007](0007-fail-open-vs-fail-closed.md), [0008](0008-eval-corpus-and-judge.md), [0011](0011-fork-prs-and-untrusted-content.md), [0012](0012-local-vs-ci-authentication.md), [0013](0013-policy-layering.md)

## Context

The candidate starting point is Anthropic's `claude-code-security-review` action (reviewed at commit `0c6a49f`, 2026-02-11, the head of `main` when checked). It is a composite action. It fetches the PR and its diff from the GitHub API, runs Claude Code headless on the checkout to find candidates, drops candidates with regex hard exclusions, sends each remaining candidate to the Messages API for false-positive filtering, and posts the survivors as inline review comments. Two inputs let a caller add instructions: `custom-security-scan-instructions` is appended to the find prompt, and `false-positive-filtering-instructions` replaces the filter's default instructions.

Reading the source showed behaviour that the SIA use case has to work around:

1. On an error, the scan step writes `{"error": ...}` and reports `findings_count=0`, and the job still passes. A model outage therefore looks like a clean review.
2. The filter's startup check calls the hard-coded model `claude-3-5-haiku-20241022`. If that call fails, the filter turns itself off with a log warning, and every candidate is posted unfiltered. Whether that model still answers is Unknown. It is the first thing to verify ([0012](0012-local-vs-ci-authentication.md)).
3. By default it reviews only the first commit of a PR (`run-every-commit: false`). Separately, its comment script posts nothing if any earlier bot security comment exists on the PR.
4. It installs `@anthropic-ai/claude-code` from npm without a version pin.
5. The scan step holds `GITHUB_TOKEN` and the model credential in the same environment as Claude Code ([0011](0011-fork-prs-and-untrusted-content.md)).

## Considered options

### Option A: Composite action that wraps the upstream action at a pinned commit

- Pro: A maintained reviewer with a tested prompt, filter and PR commenting, usable today with nothing to port or remove. Upstream fixes arrive by bumping one SHA.
- Pro: The reference design (Figma) started the same way: this action in shadow mode, with the policy tuned on top.
- Pro: Instructions enter only through the two supported inputs, so the upstream code stays untouched.
- Con: Items 3 to 5 can only be worked around in the wrapper's own steps, not fixed.

### Option B: Fork the upstream action

- Pro: All five items can be fixed.
- Con: We own a diverging copy, and every upstream change becomes a manual merge.

### Option C: In-house reviewer that calls Claude Code headless (`claude -p`) directly

- Pro: Full control of prompt, tools, passes and authentication. This is the route the reference design (Figma) took after starting from this action.
- Con: The upstream prompt, filter instructions and regex exclusions all have to be ported and then kept in sync by hand. Building it first means validating a new reviewer before any eval data shows it beats the upstream one.

### Option D: Custom review service (GitHub App plus job queue)

- Pro: Central retries, telemetry and state across PRs.
- Con: Hosting and on-call for a service, none of which is needed to show review quality.

## Decision

Option A, as the first option. It gets a working reviewer on every PR soonest, and its eval numbers become the baseline that Options B, C and D have to beat. `action/action.yml` is a composite action with these steps:

1. Refuse fork PRs ([0011](0011-fork-prs-and-untrusted-content.md)).
2. Render the policy ([0013](0013-policy-layering.md)) into the two instruction files.
3. Remove agent configuration files (`.claude/`, `CLAUDE.md`, `.mcp.json`) from the checked-out working tree ([0011](0011-fork-prs-and-untrusted-content.md)).
4. Call `anthropics/claude-code-security-review@0c6a49f...` with `run-every-commit: true`, a current `claude-model`, and the rendered files.
5. Verify the outcome. The step reads `claudecode-results.json` and `claudecode-error.log`, and fails the check if the scan errored or if the filter disabled itself ([0007](0007-fail-open-vs-fail-closed.md)), or, when blocking is on, if a HIGH finding was reported ([0006](0006-required-check-semantics.md)).
6. Write every kept finding to the job summary. This is done because item 3 can suppress inline comments on later pushes.

The eval harness ([0008](0008-eval-corpus-and-judge.md)) runs the upstream `main()` at the same SHA, replacing only the two methods that fetch PR data from GitHub.

## Consequences

- Upstream is pinned by SHA, so a bump is a reviewed PR that includes an eval run.
- The unpinned Claude Code npm install inside upstream remains. The Claude Code version can change between runs without a diff on our side.

## Future opportunities

- **Adjudication pass.** Upstream writes dropped candidates to `filtering_summary.excluded_findings_details`. A wrapper step after the upstream action could re-examine them ([0005](0005-models-and-second-reviewer.md)) without forking.
- **Upstream contributions** for items 1 to 4: a non-zero exit on scan error, a configurable or removed validation model, posting new findings on later pushes, and a pinned Claude Code version. Each one merged upstream deletes a workaround here.
- **Option C as a measured alternative.** The harness can run a headless variant with the same policy next to the upstream pipeline on the same corpus. Switch only if the eval shows higher recall at the same false-positive ceiling.
- **Option D** when org-wide telemetry, fix tracking or cross-PR state becomes a requirement.
