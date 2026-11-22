# 0005. CRITICAL and HIGH findings block the merge until fixed or disputed; disputes are reviewed afterwards

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-reviewer-core.md), [0002](0002-classifier-vs-single-policy.md), [0006](0006-fail-open-vs-fail-closed.md), [0008](0008-measuring-precision.md), [0009](0009-rollout-gates.md), [0011](0011-policy-layering.md)

**In short:** a PR cannot merge while the review reports a CRITICAL or HIGH finding. MEDIUM and LOW findings are comments only. When the author thinks such a finding is wrong, they raise a false-positive request on the PR. The Adaptive Security team decides, and a dismissed finding is not reported or blocking again on that PR. Raising the request releases the block at once; nobody waits on a person. A ruleset bypass remains for emergencies. Blocking turns on only after the rollout has measured how often CRITICAL and HIGH findings are right.

## Context

An org ruleset makes the `adaptive-security-reviewer` workflow a required status check on every PR to protected branches. The question is what makes that check fail, and how a wrong finding stops blocking.

The reviewer rates each finding CRITICAL, HIGH, MEDIUM or LOW ([design](../design.md), severity scale). CRITICAL means exploitable by an anonymous or any logged-in user with no special conditions, leading to code execution, account takeover or bulk data exposure. HIGH is directly exploitable but limited by a precondition or by impact. The upstream prompt has no CRITICAL level, so the scale belongs to the reviewer core's own prompt ([0001](0001-reviewer-core.md)). The reference design (Figma) decided not to block merges on findings, because of the friction, and tracked fix rates instead. Here the requirement is the opposite: a change with a serious vulnerability must not merge ([design](../design.md), R6). Three further requirements shape the decision:
- Nobody waits on another person to unblock their PR (R6).
- A PR author can report a finding as a false positive from the PR, and a dismissed finding is not reported or blocking again on that PR (R17).
- The review supplements human code review, which every PR already has (R3).

Two properties of the reviewer matter:
- Every push gets a fresh review of the whole PR, with no memory of earlier runs. A fixed finding disappears on the next run and stops blocking. A wrong finding is usually raised again on every push, so a dismissal must be remembered outside the reviewer, and matched to new findings even though the model rewords them.
- How often CRITICAL and HIGH findings are wrong on real code is Unknown until the rollout measures it ([0008](0008-measuring-precision.md)).

A required check needs a result on every head commit, so the review runs on every push.

## Considered options

### Option A: Findings never block; the check fails only when the review could not run

- Pro: Lowest friction.
- Con: A known exploitable bug can merge.

### Option B: CRITICAL and HIGH findings block; the Adaptive Security team bypasses wrong ones

- Pro: Uses the ruleset's built-in bypass list, so there is nothing to build, and GitHub records each bypass.
- Con: Every wrong blocking finding waits for a person to merge the PR, with no limit on how long. The same finding comes back on the next push.

### Option C: CRITICAL, HIGH and MEDIUM findings block

- Con: MEDIUM findings depend on conditions the reviewer often cannot check, so more PRs would be wrongly blocked.

### Option D: CRITICAL and HIGH findings block; an author's false-positive request releases the block, and the team reviews it afterwards

- Pro: A wrong finding is settled once per PR, not on every push. The author has a defined way to dispute it from the PR.
- Pro: No engineer waits on the Adaptive Security team, and the team reviews requests at its own pace.
- Con: An author can dispute a real bug to merge it. Every request is reviewed, a rejected one after merge becomes a follow-up fix with an owner, and requests are visible per author and repository.
- Pro: Every decision is a labelled data point for precision ([0008](0008-measuring-precision.md)).
- Con: Needs state outside the reviewer (the dismissals), a way to match findings across runs, and a way to verify that the approver belongs to the Adaptive Security team.

## Decision

Option D.

- **Blocking switch.** The wrapper input `fail-on-severity` takes `none` or `high`. The workflow passes `${{ vars.ASR_FAIL_ON_SEVERITY || 'none' }}`, so blocking is switched on org-wide by one organization variable, at the rollout phase where precision supports it ([0009](0009-rollout-gates.md)).
- **What fails the check.** With `high`, the check fails when the completed review reports a CRITICAL or HIGH finding that is neither dismissed nor under a pending false-positive request. It also fails when the review could not run ([0006](0006-fail-open-vs-fail-closed.md)), and for fork PRs, which GitHub runs without repository secrets.
- **The wrapper posts the comments, not upstream.** It reads the review's results after every run and gives each finding a fingerprint. On every run, the posting step:
  - posts only findings that are not already on the PR and not dismissed;
  - replies "no longer reported in `<sha>`" to findings that disappeared, and resolves their threads;
  - keeps one summary comment up to date once a PR has had findings: it lists the current findings, says when all of them are resolved, and explains how to raise a false-positive request when a CRITICAL or HIGH finding blocks the merge. A PR that never had findings gets no comment ([0002](0002-classifier-vs-single-policy.md)).
- **False-positive requests** follow the process below. An approved request dismisses the finding for that PR.
- **A request releases the block.** While a false-positive request is pending, its finding is a warning, not a failure. If the team rejects the request while the PR is still open, the finding blocks again on the next run.
- **Bypass as fallback.** The required check sits in its own ruleset, whose only bypass actor is the Adaptive Security team. A bypass then skips only this check, not other branch protections such as required approvals. It is used for emergencies, such as a hotfix during an incident, and for findings that cannot be matched across runs.

## False-positive process

1. **The author raises a request** by replying to the finding's comment with a fixed command and a reason, for example `/asr false-positive <why it is wrong>`. The request is recorded, and the Adaptive Security team is notified in [Adaptive Security team channel]. Nothing else is required of the author.
2. **A member of the Adaptive Security team decides,** usually the rotation engineer, with an approving or rejecting command on the same thread. The wrapper checks that the person deciding belongs to the team.
3. **Approved:** the finding is dismissed for that PR. Later reviews do not post it and do not block on it, as long as it is matched to the dismissed finding. **Rejected:** the finding stays, and the author fixes it.
4. **Every decision is recorded** with the finding, the reason and the decision, and feeds the monitoring ([design](../design.md), R16). Approved requests count as false positives, and rejected ones as confirmed findings ([0008](0008-measuring-precision.md)). When the same kind of false positive repeats, a team member opens a policy PR with a new precedent, or adds an eval case.
5. **[Adaptive Security team lead] reviews all dismissals and bypasses every [period],** and checks that each has a recorded reason.

## Known gaps in PR comments

The upstream comment script, as observed in a test run ([0001](0001-reviewer-core.md)), behaves like this after the first review of a PR:

- **Fixed findings are not closed.** Comments from earlier reviews stay on the PR. The reviewer has no memory of them, so it does not reply that a finding is fixed or resolve the thread. GitHub marks a comment "Outdated" only when the exact line it points at changes, so a fixed finding can still look current.
- **A clean re-review is silent.** Nothing on the PR says that a later review found nothing. The only record is the results artifact and the job summary.
- **New findings on later pushes are not posted.** The comment script posts nothing if the bot has already commented on the PR. With blocking on, a blocking finding introduced in a later push would fail the check without appearing anywhere in the PR conversation.

The wrapper's posting step replaces upstream's comment script and closes all three gaps. It needs no change to the upstream code.

## Consequences

- An engineer whose PR is wrongly blocked unblocks it themselves by raising a request. The Adaptive Security team's review happens afterwards, so its pace never holds up a merge.
- A real bug can merge if its author disputes it and the team rejects the request only after the merge. That case becomes a follow-up fix with an owner.
- The dismissals and requests are state that outlives a review run. They need a store, which is part of the results store in the design (R16).
- Since a fixed finding stops blocking on the next push, "fixed" means "the reviewer no longer reports it". A fix that only rewords the code so the model stops noticing would also pass. Human code review remains the control for that.
- A repository admin can override `ASR_FAIL_ON_SEVERITY` for their own repository with a repository variable, which switches off blocking there. [0009](0009-rollout-gates.md) describes this risk.
- The check is red on every bypassed PR, so "red check, merged" is the signal the periodic review looks for.

## Open questions

- [decide: how a rejected request on an already merged PR is tracked as a follow-up fix, and who owns it.]
- [decide: how a finding is matched across runs. The model rewords descriptions between runs, so the fingerprint cannot be the text. A candidate is file, category and a line window, with the risk of merging two different findings in the same area.]
- [decide: how the wrapper verifies that the person approving a request belongs to the Adaptive Security team. The workflow's own token cannot read team membership, so this likely needs a GitHub App owned by the team.]
- [decide: whether a bypass record gets a type (`emergency` or `unreviewable`), so that bypasses are not counted as false positives ([0008](0008-measuring-precision.md)).]
- [decide: what happens to fork PRs. Every fork PR needs a bypass, which is daily friction in repositories with outside contributors. Alternatively, forking is disabled org-wide.]
- [decide: what happens on `merge_group` events. Upstream writes no results file for any event other than `pull_request`, so under fail-closed the merge queue would always fail.]
- [decide: what happens with PRs too large to review. Upstream reviews only the first 100 changed files, and a very large diff may fail to download.]
- [decide: whether the model separates CRITICAL from HIGH consistently enough to be useful for triage. The eval should label the expected severity of each case and report agreement.]
- [decide: the precision of CRITICAL and HIGH findings required before setting `ASR_FAIL_ON_SEVERITY=high`. The starting proposal is [X]%, measured in phase 2 ([0009](0009-rollout-gates.md)).]

## Future opportunities

- **Dismissals across PRs.** A finding approved as a false positive on one PR could stay dismissed for the same code in later PRs of the same repository. That needs matching across PRs, which is harder than within one PR.
