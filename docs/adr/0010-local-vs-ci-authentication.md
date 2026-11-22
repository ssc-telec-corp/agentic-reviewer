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
- The Anthropic API supports workload identity federation (WIF). An API organization registers GitHub's OIDC issuer (`https://token.actions.githubusercontent.com`) and a rule that matches claims such as `repository_owner`, and maps it to a service account in a workspace. The Anthropic SDKs and the `ant` CLI then exchange the job's GitHub identity token for a short-lived Anthropic token, configured through `ANTHROPIC_FEDERATION_RULE_ID`, `ANTHROPIC_ORGANIZATION_ID`, `ANTHROPIC_SERVICE_ACCOUNT_ID` and `ANTHROPIC_IDENTITY_TOKEN_FILE`. WIF belongs to an API organization (Console, pay-as-you-go). A Claude subscription does not include one.

Assumptions to verify:

- **A1 (checked, false).** `claude-3-5-haiku-20241022` was retired on 2026-02-19, so the upstream filter never runs ([0001](0001-composite-action-vs-custom-service.md), problem 1).
- **A2 (checked, true).** When `ANTHROPIC_API_KEY` is set, Claude Code uses it as an API key, even if `CLAUDE_CODE_OAUTH_TOKEN` is also set. In a test run on 2026-11-22, a `claude setup-token` subscription token was put in both. Claude Code sent it as an API key and got `401 Invalid API key`. The unmodified upstream action therefore cannot run on a Claude subscription in any configuration, because it refuses to start without `ANTHROPIC_API_KEY`.
- **A3.** Claude Code (`claude -p`) accepts the WIF variables. The SDK and `ant` CLI documentation says they do the exchange. Whether Claude Code does is Unknown, and needs checking before WIF is used for the scan step.

## Considered options

### Option A: API key everywhere

- Pro: The only way to run the upstream pipeline unmodified, locally and in CI. POC and production differ only in which key.
- Con: The POC needs a personal Console key with billing, separate from the subscription.

### Option B: Subscription locally, API key in CI

- Con: Locally, upstream refuses to start. Patching that check out in the harness would measure a pipeline that differs from CI.

### Option C: Workload identity federation in CI, no stored key

- Pro: No long-lived key exists, so none can leak through the review step, where the reviewed code runs next to the credentials ([0001](0001-composite-action-vs-custom-service.md), problem 7). Nothing to rotate.
- Pro: One rule matching `repository_owner: [org]` covers every repository the required workflow runs in.
- Con: The upstream code cannot use it unchanged. Its startup check requires `ANTHROPIC_API_KEY`, and its filter passes that key explicitly to the SDK. An `ANTHROPIC_API_KEY` takes precedence over the WIF variables, so a placeholder key would break the exchange. It needs a patch, which [0001](0001-composite-action-vs-custom-service.md) Options B, C and D allow.
- Con: Depends on A3 for the scan step.

## Decision

Option A, for the upstream code. If [0001](0001-composite-action-vs-custom-service.md) chooses a headless reviewer that makes every model call through Claude Code, local runs can also use a subscription login, and production still uses the API key.

| Mode | Credential | Source |
|---|---|---|
| Local eval (POC) | `ANTHROPIC_API_KEY` | A personal Anthropic Console key with a spend limit of [budget], exported in the shell |
| CI eval and PR review (POC) | `ANTHROPIC_API_KEY` | A repository secret in the POC repo |
| Production | `ANTHROPIC_API_KEY` | An organization secret from the company Console org [org-name], in a workspace dedicated to the SIA review with spend limit [budget]. To be replaced by WIF (Option C) once the reviewer core allows it and A3 is confirmed |

The eval judge also uses the key. Upstream requires `anthropic>=0.39.0`, an open range, so the harness pins an exact version of the SDK itself.

## Consequences

- Moving to production means creating the Console workspace and key, storing it as an org secret visible to every repo covered by the ruleset, and setting the spend alert in the [runbook](../runbook.md).
- POC cost numbers are list-price figures from Claude Code, plus token-count estimates for the filter calls. Negotiated pricing is Unknown.

## Future opportunities

- **Workload identity federation in production (Option C).** Preferred over a stored key. It needs the reviewer-core change in [0001](0001-composite-action-vs-custom-service.md) and a check of A3.
- A headless variant ([0001](0001-composite-action-vs-custom-service.md)) could run on subscription credentials for individual developers' local pre-PR checks.
