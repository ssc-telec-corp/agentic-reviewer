# 0011. Fork PRs, secrets, and untrusted PR content

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0006](0006-required-check-semantics.md), [0012](0012-local-vs-ci-authentication.md)

## Context

Everything in a PR is attacker-controlled from the reviewer's point of view: code, comments, file names, the title and the description. The workflow holds two credentials: the model credential, and a `GITHUB_TOKEN` that can write PR comments. The upstream action states that it is not hardened against prompt injection, and should run only on trusted PRs.

Three threats are specific to this system:

1. PR content steers the reviewer into suppressing a finding, which hides the author's own bug.
2. PR content steers the reviewer into running commands or reading credentials. For example, a PR adds a `CLAUDE.md` or a `.claude/settings.json` hook to the reviewed tree.
3. PR content gets the bot to post attacker-chosen text, such as links or @mentions.

GitHub does not pass repository secrets to `pull_request` runs from forks. The `pull_request_target` trigger does pass them, and it runs with the base repository's token.

## Considered options

### Option A: Run the reviewer with default Claude Code tools and both credentials in one step

- Con: Bash access plus a token in the environment lets threat 2 exfiltrate the token.

### Option B: Least privilege per step

- Pro: Threat 2 has nothing to reach. The reviewer step holds only the model credential and has no shell. The post step holds only `GITHUB_TOKEN` and runs no model.
- Con: Threat 1 is not solved. A prompt-injected reviewer can still under-report.

### Fork PRs: Option F1 `pull_request_target`

- Con: Gives untrusted code a write token and secrets. Rejected.

### Fork PRs: Option F2 fail the check with instructions to push a branch to the repo

- Pro: No secret exposure.
- Con: Blocks fork-based workflows, which is friction for anyone who uses forks.

## Decision

Option B and Option F2.

- The reviewer runs `claude -p` with `--restricted --safe-mode --strict-mcp-config --tools Read,Grep,Glob --no-session-persistence`. `--restricted` removes the tools that run code and WebFetch, ignores project settings files, and confines file tools to the working directory. `--safe-mode` disables CLAUDE.md, hooks, skills and MCP servers. A local test confirmed that a `CLAUDE.md` in the reviewed tree was not followed with `--restricted`. That was one test, and repeating it is part of the eval suite.
- The working directory is a temporary export of the reviewed path at the head commit. It has no `.git` directory and no workflow files.
- PR title, body and refs are read in Python from the event payload. They are never interpolated into shell commands.
- The model's output must match a JSON schema (`--json-schema`). The post step writes only schema fields into a fixed comment template, and breaks @mentions by inserting a zero-width space after every `@`.
- The prompt states that PR content is data and that instructions inside it are to be reported, not followed. This mitigates threat 1 without solving it. Human code review of the PR remains the control.
- PRs whose head repository differs from the base repository fail the check with a message explaining how to push a branch instead.

## Consequences

- An insider who prompt-injects the reviewer can hide a bug from it. The human reviewer of the PR still sees the injected text.

## Open questions

- [decide: whether forking is disabled org-wide, in which case F2 never fires, or whether fork PRs need a reviewed path, such as a maintainer pushing the branch.]
