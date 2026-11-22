# 0010. Authentication: API key in every mode

- Status: Proposed
- Date: 2026-11-22
- Related: [0001](0001-composite-action-vs-custom-service.md), [0007](0007-eval-corpus-and-judge.md)

## Context

The POC is built on a personal machine with a personal Claude subscription and no company credentials. Production uses company credentials. Facts from the upstream source and local tests:

- `validate_claude_available()` in upstream exits unless `ANTHROPIC_API_KEY` is set, before Claude Code runs. The action cannot run on subscription credentials, even for the find stage.
- The filter calls the Messages API through the `anthropic` SDK with that key.
- The filter's startup check calls `claude-3-5-haiku-20241022`. If the call fails, filtering is disabled silently ([0001](0001-composite-action-vs-custom-service.md)).
- Claude Code 2.1.311 headless reports `total_cost_usd` at list price, on either a subscription or an API key.

Assumptions to verify:

- **A1.** `claude-3-5-haiku-20241022` still answers on the Messages API. Verify on the first eval run. The wrapper's verify step and the harness both fail loudly if it does not.
- **A2.** When Claude Code has both a subscription login and `ANTHROPIC_API_KEY`, it uses the key. The harness sets the key explicitly.

## Considered options

### Option A: API key everywhere

- Pro: The only way to run the upstream pipeline unmodified, locally and in CI. POC and production differ only in which key.
- Con: The POC needs a personal Console key with billing, separate from the subscription.

### Option B: Subscription locally, API key in CI

- Con: Locally, upstream refuses to start. Patching that check out in the harness would measure a pipeline that differs from CI.

## Decision

Option A.

| Mode | Credential | Source |
|---|---|---|
| Local eval (POC) | `ANTHROPIC_API_KEY` | A personal Anthropic Console key with a spend limit of [budget], exported in the shell |
| CI eval and PR review (POC) | `ANTHROPIC_API_KEY` | A repository secret in the POC repo |
| Production | `ANTHROPIC_API_KEY` | An organization secret from the company Console org [org-name], in a workspace dedicated to the SIA review with spend limit [budget] |

The eval judge also uses the key, through the same `anthropic` SDK version that upstream pins in its requirements, so cost accounting and authentication are the same for every model call.

## Consequences

- Moving to production means creating the Console workspace and key, storing it as an org secret visible to every repo covered by the ruleset, and setting the spend alert in the [runbook](../runbook.md).
- POC cost numbers are list-price figures from Claude Code, plus token-count estimates for the filter calls. Negotiated pricing is Unknown.

## Future opportunities

- Workload identity, if Anthropic offers OIDC-based credentials for GitHub Actions. Unknown whether it does.
- A headless variant ([0001](0001-composite-action-vs-custom-service.md)) could run on subscription credentials for individual developers' local pre-PR checks.
