# 0001. Reviewer core: wrap, copy or rebuild `claude-code-security-review` (open)

- Status: Proposed, decision open
- Date: 2026-11-22
- Related: [0002](0002-classifier-vs-single-policy.md), [0003](0003-precedents-vs-rules.md), [0004](0004-models-and-second-reviewer.md), [0005](0005-required-check-semantics.md), [0006](0006-fail-open-vs-fail-closed.md), [0007](0007-eval-corpus-and-judge.md), [0008](0008-measuring-precision.md), [0010](0010-local-vs-ci-authentication.md), [0011](0011-policy-layering.md), [0012](0012-versioned-releases.md)

**In short:** the reviewer core, meaning the code that turns a PR into findings, is not decided. Anthropic's `claude-code-security-review` action is the starting point, but it has bugs that break it today and shows no sign of maintenance. Four options are open, listed below. Everything else in this design (policy, eval, workflows, releases, rollout) is the same whichever option is chosen.

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

**Test run on 2026-11-22.** The unmodified action ran on a demo PR with a planted missing-authorization bug. Claude Code failed authentication (a subscription token cannot be used, see [0010](0010-local-vs-ci-authentication.md)). The filter's startup check returned `404 not_found_error` for `claude-3-5-haiku-20241022`. The job still passed, and the PR got no comments, which confirms problems 1 and 2 in a real run.

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

### Option B: GitHub fork of the upstream repository

- Pro: Every problem can be fixed, and community PRs can be cherry-picked.
- Con: A fork of a public repository must stay public on GitHub, so our patches and wiring would be public.
- Con: A second repository with its own versioning, next to the wrapper.

### Option C: Copy upstream into our repository and change it step by step

- Pro: Starts from upstream's exact code, so nothing is lost and there is a working baseline on day one. It stays private and is released with the wrapper ([0012](0012-versioned-releases.md)).
- Pro: It can move gradually toward Option D, with each step checked by the eval:
  1. Fix problems 1, 2 and 6.
  2. Run Claude Code with `--restricted --safe-mode`, restricted tools and `--json-schema`.
  3. Separate the credentials into different steps, and post new findings on later pushes.
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
- Pro: Runs on a subscription login locally and an API key in production, with no code change ([0010](0010-local-vs-ci-authentication.md)). This is where the reference design, Figma's, ended up.
- Con: New code that has to be written and proven by the eval before anyone trusts it.
- Con: The copied instructions can drift from upstream. It also depends on Claude Code CLI behaviour, so the CLI version must be pinned.

## Decision

Not decided. [decide: Option B, C or D. Option A is not viable because of problem 1. The inputs to the decision are the maintenance evidence above, whether a working review is needed quickly or the long-term design matters more, and the company's rules for third-party code.]

Until this is decided, the other ADRs describe the wrapper steps against upstream's interface (two instruction files, `claudecode-results.json`, `claudecode-error.log`). Options B and C keep that interface. Option D replaces it, and those ADRs would need updating.

## Consequences

- No reviewer can run on PRs until this is decided. With Option A, the filter stage is missing.
- The eval harness ([0007](0007-eval-corpus-and-judge.md)) can be built either way. Its input is a commit and its output is a list of findings.

## Future opportunities

- **A custom review service** (GitHub App plus job queue), when org-wide telemetry, fix tracking or state across PRs becomes a requirement. The reference design moved to such a service.
