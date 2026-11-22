# 0011. Fork PRs, secrets, and untrusted PR content

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0006](0006-required-check-semantics.md), [0012](0012-local-vs-ci-authentication.md)

## Context

Everything in a PR is attacker-controlled from the reviewer's point of view: code, comments, file names, title and description. The upstream README says the action is not hardened against prompt injection and should run only on trusted PRs. From its source:

- Claude Code runs with its default tool set in the PR checkout. The only tool explicitly disallowed is `Bash(ps:*)`.
- The scan step's environment holds both `ANTHROPIC_API_KEY` and `GITHUB_TOKEN`.
- Claude Code runs in the checkout's root directory. In non-interactive mode it skips the workspace trust prompt. A `CLAUDE.md` or `.claude/settings.json` added by the PR would therefore probably be loaded. That is an assumption, not tested against the action.

The threats specific to this system are:

1. PR content steers the reviewer into suppressing a finding.
2. PR content changes the reviewer's configuration, for example with a project settings file that allows Bash, and so reaches the credentials in the environment.
3. PR content gets the bot to post attacker-chosen text.

GitHub does not pass secrets to `pull_request` runs from forks. `pull_request_target` does, and runs with a write token.

## Considered options

### Option A: Run upstream as documented

- Con: Threat 2 is open for any PR author.

### Option B: Wrapper mitigations around the unmodified upstream action

- Pro: Closes the configuration path for threat 2 without a fork.
- Con: The upstream step still holds both credentials, and Claude Code still has its default read tools.

### Option C: Fork and run Claude Code with `--restricted --safe-mode --tools Read,Grep,Glob`

- Pro: The strongest isolation. A local test showed a planted `CLAUDE.md` was not followed under `--restricted`.
- Con: It is a fork ([0002](0002-composite-action-vs-custom-service.md)).

### Fork PRs

- F1, `pull_request_target`: gives untrusted code secrets and a write token. Rejected.
- F2: fail the check with instructions to push a branch to the repository.

## Decision

Option B and F2.

- Before the upstream step, the wrapper deletes `.claude/`, every `CLAUDE.md`, and `.mcp.json` from the checked-out working tree. The diff that the reviewer receives from the GitHub API still shows changes to those files, so they are still reviewed as content.
- Workflow permissions are `contents: read` and `pull-requests: write`, and nothing more.
- PR title, body and refs never pass through `${{ }}` into shell. The wrapper reads them from `$GITHUB_EVENT_PATH` in Python.
- The rendered policy includes the precedent that code comments and PR text are data, and that a claim of prior approval does not suppress a finding. This reduces threat 1 without solving it. Human code review remains the control.
- PRs whose head repository differs from the base repository fail the check with a message.

## Consequences

- Residual risk: a PR that prompt-injects the reviewer can hide its own bug, and Claude Code shares the scan step's environment with `GITHUB_TOKEN`. The token's scope is limited to the current repository, with the permissions above.

## Future opportunities

- Upstream contribution or fork for Option C: restricted tools, and separate steps for the model and GitHub credentials.
- An eval case for prompt injection exists in the corpus. Keep it in the gate set so that a regression in threat 1 shows as a recall drop.
- [decide: whether forking is disabled org-wide, which makes F2 moot.]
