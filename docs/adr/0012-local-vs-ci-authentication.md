# 0012. Authentication: every model call goes through the Claude Code CLI

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0011](0011-fork-prs-and-untrusted-content.md)

## Context

The POC was built and measured with a personal Claude subscription and no company credentials. Production will use company credentials. The reviewer, the adjudicator and the eval judge all need to call a model, both locally and in GitHub Actions.

Checked facts:

- The upstream action's filter stage calls the Messages API through the `anthropic` SDK. It can only authenticate with an API key, so it cannot use a subscription.
- Claude Code 2.1.311 in headless mode (`claude -p`) runs on the local subscription login. It reports `total_cost_usd` with `costBasis: list`, which is the list-price equivalent rather than an amount billed.
- `claude --bare` ignores OAuth and keychain credentials and accepts only `ANTHROPIC_API_KEY`. The reviewer therefore does not use `--bare`.
- `claude setup-token` creates a long-lived token for a subscription account.

Assumptions not yet verified:

- **A1.** In GitHub Actions, Claude Code authenticates with the token from `claude setup-token` when it is supplied as `CLAUDE_CODE_OAUTH_TOKEN`. Verify by running the CI eval workflow.
- **A2.** Subscription terms allow this automated use for a personal proof of concept. Verify against the current consumer terms before running the CI workflows. It is not a production option either way.
- **A3.** When both `ANTHROPIC_API_KEY` and `CLAUDE_CODE_OAUTH_TOKEN` are set, which one wins is Unknown. The action sets only one.

## Considered options

### Option A: Messages API with an API key everywhere

- Con: The POC could not run locally on a subscription.

### Option B: Every model call goes through `claude -p`, and credentials come from the environment

- Pro: The same code authenticates with a local login, an OAuth token or an API key. No code changes between POC and production.
- Con: Each call has Claude Code start-up time and a Node.js dependency.

## Decision

Option B.

| Mode | Credential | Where it comes from |
|---|---|---|
| Local eval (POC) | Subscription login | `claude` interactive login on the developer machine |
| CI eval and PR review (POC) | `CLAUDE_CODE_OAUTH_TOKEN` (A1, A2) | GitHub secret in the POC repository |
| Production | `ANTHROPIC_API_KEY` | Organization secret holding a key from the company Anthropic Console org [org-name], in a workspace with a spend limit [budget] |

The action accepts either `anthropic-api-key` or `claude-code-oauth-token` as an input, and fails if both or neither are set (A3).

## Consequences

- Moving to production means creating the Console workspace and key, storing it as an org secret visible to all repos covered by the ruleset, and removing the OAuth token secret.
- Cost numbers from the POC are list-price equivalents, and production billing at list price should match them. Any negotiated pricing is Unknown.
