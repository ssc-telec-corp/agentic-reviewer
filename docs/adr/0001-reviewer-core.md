# 0001. Reviewer core: copy or rebuild `claude-code-security-review` (open)

- Status: Proposed, decision open between Options C and D
- Date: 2026-11-23
- Related: [0002](0002-classifier-vs-single-policy.md), [0003](0003-precedents-vs-rules.md), [0004](0004-models-and-second-reviewer.md), [0005](0005-required-check-semantics.md), [0006](0006-fail-open-vs-fail-closed.md), [0007](0007-eval-corpus-and-judge.md), [0008](0008-measuring-precision.md), [0010](0010-local-vs-ci-authentication.md), [0011](0011-policy-layering.md), [0012](0012-versioned-releases.md)

**In short:** the reviewer core, meaning the code that turns a PR into findings, must be changed from Anthropic's `claude-code-security-review` action, because the action cannot meet the requirements below unchanged, and it shows no sign of maintenance. Using it unchanged (Option A) and a public GitHub fork (Option B) are ruled out. The open choice is between copying it into our repository and changing it (Option C) or building our own reviewer on headless Claude Code (Option D). Everything else in this design (policy, eval, workflows, releases, rollout) is the same whichever is chosen.

## Context

`claude-code-security-review` is a composite GitHub Action, MIT-licensed, copyright 2025 Anthropic. It was reviewed at commit `0c6a49f` (2026-02-11). It works in three steps:
1. Claude Code reads the PR diff and repository, and lists possible vulnerabilities.
2. Regex rules drop findings by keyword.
3. One Messages API call per remaining finding filters out false positives.

It then posts the survivors as inline PR comments. It fetches the PR from the GitHub API and needs `ANTHROPIC_API_KEY`.

**Problems found in its source**, upstream file and line numbers at `0c6a49f`:

1. **The filter never runs.** Its startup check calls the hard-coded model `claude-3-5-haiku-20241022` (`claude_api_client.py:61-66`), which was retired on 2026-02-19. The check fails, the filter switches itself off with a log warning (`findings_filter.py:187-192`), and every finding is posted unfiltered.
2. **A failed review looks clean.** If Claude Code's output is not the expected JSON, the scan returns an empty findings list with `review_completed: false`, and no error (`github_action_audit.py:289-312`). On an explicit error, the action writes `findings_count=0`, and the job passes.
3. **A failed filter call keeps the finding** unfiltered, marked `Claude API failed` (`findings_filter.py:297-306`).
4. **Later pushes go unreported.** By default it reviews only the first commit of a PR, and its comment script posts nothing if the PR already has a bot security comment.
5. **Only the first 100 changed files** are fetched (`github_action_audit.py:77-81`).
6. **Instruction files given as relative paths are skipped,** with only a warning, because the scan runs from the action's own directory.
7. **Credentials share the scan's environment.** The scan step holds `GITHUB_TOKEN` and the API key next to Claude Code, which reads files the PR author wrote. Only `Bash(ps:*)` is disallowed.
8. **Unpinned installs.** It installs Claude Code from npm and its Python dependencies (for example `anthropic>=0.39.0`) without version pins.
9. **No cost data.** Claude Code reports a cost for each run, but the action discards it, and the filter's API calls report nothing. Neither the eval nor budget alerts get a per-review cost from the action.
10. **Deprecated runtime in its own steps.** It pins `actions/checkout`, `actions/cache`, `actions/setup-node`, `actions/setup-python` and `actions/upload-artifact` at versions built for Node.js 20, which GitHub is retiring. Every run shows a deprecation warning.

**Observed in test runs** on a demo PR with a planted vulnerability:
- With a credential the API rejected, the scan failed, and the job still passed with no comments (problem 2).
- The filter's startup check returned `404 not_found_error` for the retired model, so findings were posted unfiltered (problem 1).
- With a working credential, the scan found the planted vulnerability and described its root cause and fix correctly.

**Requirements the unchanged action cannot meet:**
- **The false-positive filter runs.** This needs a code change (problem 1).
- **Credentials come from workload identity federation, with no stored API key** ([0010](0010-local-vs-ci-authentication.md)). This needs a code change: the action refuses to start without `ANTHROPIC_API_KEY`, and it passes that value as an API key, which takes precedence over federation.
- **A review that did not complete never looks like a clean one** ([0006](0006-fail-open-vs-fail-closed.md)). The wrapper can detect some cases from the action's output files, but the reliable fix is in the code (problems 2 and 3).
- **Four severity levels, including CRITICAL** ([0005](0005-required-check-semantics.md)). The action's prompt and output define only HIGH, MEDIUM and LOW, so this needs a change to the prompt.
- **Findings are tracked across pushes:** new findings are posted, and findings a later scan no longer reports are closed. The wrapper's own posting step can do this with the action's commenting switched off ([0005](0005-required-check-semantics.md)), so this one needs no change to the action itself.

**Maintenance evidence**, from the public GitHub pages on 2026-11-22:
- No PR has been merged since 2026-02-11. Eleven PRs have been merged in total.
- Problem 1 was reported on 2026-02-19 (issue #69), and in at least seven more issues since.
- About ten community PRs fix problem 1 (for example #73, #92, #128). None is merged.
- Other open community PRs address problems 2, 4 and 6 (#156, #169, #160).
- New PRs keep arriving, the latest on 2026-11-13.

## Considered options

### Option A: Use the upstream action unchanged, at a pinned commit

- Pro: No code to own. The prompt, filter and PR commenting are used exactly as published.
- Con: Problem 1 alone means the filter never runs. Problems 2 to 8 can only be worked around in wrapper steps around it, not fixed.
- Con: The benefit of fixes arriving through SHA bumps does not exist while nothing is merged upstream.
- **Ruled out:** it cannot meet the filter and federation requirements.

### Option B: GitHub fork of the upstream repository

- Pro: Every problem can be fixed, and community PRs can be cherry-picked.
- Con: A fork of a public repository must stay public on GitHub, so our patches and wiring would be public.
- Con: A second repository with its own versioning, next to the wrapper.
- **Ruled out:** the code would have to be public. Option C gives the same changes in a private repository.

### Option C: Copy upstream into our repository and change it step by step

- Pro: Starts from upstream's exact code, so nothing is lost and there is a working baseline on day one. It stays private and is released with the wrapper ([0012](0012-versioned-releases.md)).
- Pro: It can move gradually toward Option D, with each step checked by the eval:
  1. Fix problems 1, 2 and 6.
  2. Run Claude Code with `--restricted --safe-mode`, restricted tools and `--json-schema`.
  3. Separate the credentials into different steps, and support federation for both the scan and the filter.
  4. Optionally, replace the per-finding API filter with a second Claude Code pass.
- Con: We own about 1,400 lines of code we did not design.
- Con: MIT attribution must be kept, and [decide: whether company legal must approve third-party code intake].

### Option D: Our own reviewer on headless Claude Code (`claude -p`), with upstream's instructions copied in

- Pro: Designed for this use:
  - one code path for CI, local runs and the eval, taking a code tree and a diff, with no GitHub dependency;
  - restricted tools and safe mode, under which a planted `CLAUDE.md` was ignored in a local test;
  - output forced to a JSON schema, and explicit failure codes;
  - `--fallback-model` for outages and `--max-budget-usd` to cap cost;
  - a `criteria_touched` field for the SIA record;
  - dropped candidates as structured output, which an adjudicator needs.
- Pro: Every model call goes through Claude Code, so one credential path serves all modes: a subscription login locally, and in CI a federated short-lived token passed to Claude Code as `ANTHROPIC_AUTH_TOKEN`, or an API key ([0010](0010-local-vs-ci-authentication.md)). This is where the reference design, Figma's, ended up.
- Pro: Upstream's slash command [`.claude/commands/security-review.md`](https://github.com/anthropics/claude-code-security-review/blob/main/.claude/commands/security-review.md) already works this way: the finding pass and the false-positive filter both run as Claude Code sub-agents with read-only tools. It is the starting point, which leaves less new code than building from nothing. Its output must change from Markdown to a JSON schema.
- Con: New code that has to be written and proven by the eval before anyone trusts it.
- Con: The copied instructions can drift from upstream. It also depends on Claude Code CLI behaviour, so the CLI version must be pinned.

## Decision

Options A and B are ruled out. [decide: Option C or D. The inputs to the decision are whether a working review is needed quickly (C starts from working code) or the long-term design matters more (D), how much of upstream's code the team is willing to own, and the company's rules for third-party code.]

Until this is decided, the other ADRs describe the wrapper steps against upstream's interface (two instruction files, `claudecode-results.json`, `claudecode-error.log`). Option C keeps that interface. Option D replaces it, and those ADRs would need updating.

## Security of the review step

Whichever option is chosen, the review runs Claude Code on files the PR author controls. The wrapper:
- deletes agent configuration from the checkout before the scan (`.claude/`, every `CLAUDE.md` and `CLAUDE.local.md`, and `.mcp.json`), so a PR cannot change the reviewer's instructions, settings or tools for its own review. Option D also runs Claude Code with `--restricted --safe-mode`;
- runs with workflow permissions `contents: read` and `pull-requests: write` only;
- never puts the PR title, body or branch names into shell commands, and reads them from the event file instead;
- refuses fork PRs ([0005](0005-required-check-semantics.md));
- keeps the model credential out of the step that holds the GitHub token, where the option allows it. Option C needs a change to upstream's step layout for this, and Option D does it by design. With federation ([0010](0010-local-vs-ci-authentication.md)), the credential is also short-lived.

Accepted residual risk: a PR author can write comments or text that talk the reviewer out of reporting their own bug. The prompt tells the reviewer to treat PR content as data, but that is not guaranteed. Human code review of the PR remains the control.

## Consequences

- No reviewer that meets the requirements can run on PRs until Option C or D is built. The unchanged action can be run for experiments, without its filter.
- The eval harness ([0007](0007-eval-corpus-and-judge.md)) can be built either way. Its input is a commit and its output is a list of findings.

## Future opportunities

- **A custom review service** (GitHub App plus job queue), when org-wide telemetry, fix tracking or state across PRs becomes a requirement. The reference design moved to such a service.
