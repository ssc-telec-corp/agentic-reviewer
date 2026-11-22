# 0005. HIGH findings block the merge; security team can bypass through a defined process

- Status: Proposed
- Date: 2026-11-22
- Related: [0001](0001-composite-action-vs-custom-service.md), [0006](0006-fail-open-vs-fail-closed.md), [0008](0008-measuring-precision.md), [0009](0009-rollout-gates.md), [0011](0011-policy-layering.md)

**In short:** a PR cannot merge while the review reports a HIGH severity finding. MEDIUM and LOW findings are comments only. When a HIGH finding is wrong, the security team merges the PR through a ruleset bypass, following the process below. Blocking turns on only after the rollout has measured how often HIGH findings are right.

## Context

An org ruleset makes the `adaptive-security-reviewer` workflow a required status check on every PR to protected branches. The question is what makes that check fail.

The upstream action rates each finding HIGH ("directly exploitable: RCE, data breach, authentication bypass"), MEDIUM ("requires specific conditions") or LOW, and instructs the model to report only HIGH and MEDIUM. The reference design (Figma) decided not to block merges on findings, because of the friction, and tracked fix rates instead. Here the requirement is the opposite: a change with a vulnerability that must be fixed must not merge.

Two properties of the reviewer shape the design:
- Every push gets a fresh review of the whole PR, with no memory of earlier runs. A fixed finding disappears on the next run and stops blocking. A wrong finding is usually raised again on every push, so it cannot be dismissed once and forgotten.
- How often HIGH findings are wrong on real code is Unknown until the rollout measures it ([0008](0008-measuring-precision.md)).

A required check needs a result on every head commit, so the wrapper sets `run-every-commit: true`.

## Considered options

### Option A: Findings never block; the check fails only when the review could not run

- Pro: Lowest friction, and a wrong finding costs one reply.
- Con: A known exploitable bug can merge.

### Option B: HIGH findings block; security team bypasses wrong ones

- Pro: Exploitable bugs cannot merge unless security decides they can.
- Pro: The bypass uses the ruleset's built-in bypass list, so there is nothing to build. GitHub records each bypass.
- Con: Every wrong HIGH finding needs a security engineer. The cost depends on precision, which is why blocking waits until precision is measured.

### Option C: HIGH and MEDIUM findings block

- Con: MEDIUM findings depend on conditions the reviewer often cannot check, so more PRs would be wrongly blocked.

### Option D: Security team or author dismisses individual findings, and the wrapper remembers the dismissals

- Pro: The check turns green without a bypass.
- Con: It needs a way to recognize "the same finding" across runs even though the model rewords it each time. Verifying that the dismisser is on the security team needs a GitHub App token. If authors could dismiss, they could dismiss a real bug in their own PR.

## Decision

Option B.

- The wrapper input `fail-on-severity` takes `none` or `high`. The workflow passes `${{ vars.ASR_FAIL_ON_SEVERITY || 'none' }}`, so blocking is switched on org-wide by one organization variable, at the rollout phase where precision supports it ([0009](0009-rollout-gates.md)).
- With `high`, the check fails when the completed review reports at least one HIGH finding. The job summary and the failure message list those findings and link to the bypass process.
- The check also fails when the review could not run ([0006](0006-fail-open-vs-fail-closed.md)), and for fork PRs, which GitHub runs without repository secrets, so the review cannot run.
- The required check sits in its own ruleset, whose only bypass actor is [security-team]. A bypass then skips only this check, not other branch protections such as required approvals.

## Bypass process

When the author believes a HIGH finding is wrong:

1. **The author replies to the finding's comment** with why it is wrong, and asks for a security review in [channel] or by requesting review from [security-team]. Nothing else changes for the engineer.
2. **Security on-call decides within [SLA].** If the finding is real, the author fixes it, and the next push re-runs the review. If the finding is wrong, on-call merges the PR through the ruleset bypass.
3. **On-call records the decision** in a PR comment with a fixed form: `ASR bypass: <finding title> | reason: <why it is wrong> | follow-up: <link to policy PR or eval case, or none>`.
4. **Every bypass is a labelled false positive.** It counts against precision ([0008](0008-measuring-precision.md)). When the same kind of mistake repeats, on-call opens a policy PR with a new precedent, so the reviewer stops raising it.
5. **[security-lead] reviews all bypasses every [period]**, using GitHub's ruleset insights or the org audit log, and checks that each has a recorded reason.

The same path covers an emergency, for example a hotfix during an incident that the reviewer blocks. In that case the reason records the incident.

## Consequences

- Engineers whose PR is wrongly blocked wait for security on-call. The [SLA] and the on-call load are set by HIGH precision, which phase 2 of the rollout measures before blocking turns on.
- A repository admin can override `ASR_FAIL_ON_SEVERITY` for their own repository with a repository variable, which switches off blocking there. [0009](0009-rollout-gates.md) describes this risk and how overrides are detected.
- Since a fixed finding stops blocking on the next push, "fixed" means "the reviewer no longer reports it". A fix that only rewords the code so the model stops noticing would also pass. Human code review remains the control for that.
- The check is red on every bypassed PR, so "red check, merged" is the signal the periodic review looks for.

## Open questions

- [decide: the [SLA] for on-call decisions, and the [channel] engineers use to ask.]
- [decide: whether GitHub's delegated bypass (an author requests a bypass and a security team member approves it in the GitHub UI) is available for branch rulesets on [GitHub plan]. If it is, step 2 can use it instead of on-call merging the PR themselves. Unknown.]
- [decide: the HIGH precision required before setting `ASR_FAIL_ON_SEVERITY=high`. The starting proposal is [X]%, measured in phase 2 ([0009](0009-rollout-gates.md)).]

## Future opportunities

- **Option D for security engineers only**, if the bypass volume becomes a burden: stable finding fingerprints plus a dismissal command, checked against [security-team] membership.
- **Post only new findings on later pushes**, so the PR conversation matches what blocks it. Today, the upstream comment script posts nothing if the bot has commented before, so the job summary is the current list.
