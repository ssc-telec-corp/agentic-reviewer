# 0011. Policy: upstream defaults only for now; org precedents and repo overlays planned

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-composite-action-vs-custom-service.md), [0002](0002-classifier-vs-single-policy.md), [0003](0003-precedents-vs-rules.md), [0005](0005-required-check-semantics.md), [0007](0007-eval-corpus-and-judge.md), [0012](0012-versioned-releases.md)

**In short:** for now, the review uses only the default rules that ship with `claude-code-security-review`, plus the list of SIA criteria. Org precedents exist as switched-off drafts in `policy/base/`. Repository overlays, kept in this repository and owned by [security-team], are designed below but not built. Both get filled in after the project moves to [org].

## Context

The review runs on every repository in [org], across different stacks and owners. The upstream action already ships default rules: 16 exclusions, 4 signal-quality questions and 17 precedents, such as "environment variables are trusted" and "client-side checks are not security". Some of the knowledge it lacks holds org-wide, for example "a webhook must verify its caller". Some belongs to one system, for example "the notes service makes outbound calls only through OutboundClient".

The upstream action takes instructions as two text files per run. `custom-security-scan-instructions` is appended to the scan prompt. `false-positive-filtering-instructions` replaces the filter's default rules instead of adding to them. If no filtering file is passed, the filter uses its defaults unchanged.

Upstream ships two rule sets, and they differ. The slash command [`.claude/commands/security-review.md`](https://github.com/anthropics/claude-code-security-review/blob/main/.claude/commands/security-review.md) holds a scan prompt and false-positive rules, and is the main prompt for this design. The action's Python code holds its own copies (`prompts.py` for the scan, `claude_api_client.py` for the filter) and adds regex exclusions on top. Compared with the action's copy, the command's rules exclude secrets on disk only "if they are otherwise secured", exclude GitHub workflow input issues only when not triggerable by untrusted input, and drop the rule against reporting dependencies missing from the public registry. They add exclusions for regex injection, ReDoS, documentation files and missing audit logs. Findings are kept only at confidence 8 or above.

Writing precedents for a company's systems needs that company's code and incidents, so it cannot be done in a proof of concept built on synthetic code.

## Considered options

### Option A: Upstream defaults only, with org precedents and overlays added later

- Pro: Nothing to maintain now. The filter runs exactly as Anthropic ships it.
- Pro: The eval measures upstream as a baseline, which shows later how much each added precedent improves results.
- Con: Upstream knows nothing about our systems, so precision on real repositories is expected to be lower.

### Option B: Org base policy now, no overlays

- Pro: Org-wide knowledge from day one.
- Con: Precedents written against a synthetic app would have to be rewritten for real systems.

### Option C: Org base policy plus repository overlays now

- Pro: System-specific knowledge from day one.
- Con: The most to build and maintain, before any data shows it is needed.

## Decision

Option A.

- The wrapper passes only scan instructions. They contain the nine SIA criteria from `policy/base/criteria/*.yaml` (id, name, what each covers) and the instruction to set each finding's `category` to a criterion id ([0002](0002-classifier-vs-single-policy.md)).
- It passes no filtering file, so upstream's filter uses its default rules unchanged.
- The reference rule set is the slash command's. Whichever reviewer core [0001](0001-composite-action-vs-custom-service.md) chooses should use its text, so that every option is measured against the same rules. Option D uses the command file directly. With Option C, the action's prompt and filter text would be replaced by the command file's.
- The precedents in `policy/base/` are drafts and are not loaded while `precedents_enabled: false` is set in `policy/base/global.yaml`. `policy/examples/repo-overlay.yaml` shows the overlay format and is not loaded either.

## Plan for when the policy is filled in

This is the design to use when [org] enables precedents. It needs its own ADR, superseding this one, when it is built.

- **Upstream's defaults come first and win.** Our precedents only add to them. A precedent that repeats an upstream default is not added, and one that contradicts an upstream default is removed or reworded ([0003](0003-precedents-vs-rules.md)).
- **Org base policy.** This means `policy/base/`, owned by [security-team], stack-agnostic, and changed only through PRs that include an eval run. A policy change is a MINOR release ([0012](0012-versioned-releases.md)).
- **Rendering.**
  - **Scan instructions:** the criteria list, the system description and trust boundaries, then all precedents.
  - **Filtering instructions:** upstream's default text unchanged, then the line "Additional precedents for [org]. They add to the rules above; where they seem to conflict, the rules above apply.", then the same precedents.
  - The default text is stored in the repository. A test compares it with upstream's `_generate_single_finding_prompt` output at the pinned SHA, and fails after an upstream bump until the copy is updated.
- **Repository overlays live in the central repository, owned by [security-team].** An overlay is an optional `policy/repos/<repo-name>.yaml` in the reviewer repository. It holds `system`, `trust_boundaries` and criterion-tagged `precedents`. It can only add precedents, which are rendered after the base ones under "Repository-specific". The wrapper picks the file that matches `github.repository` from its own copy of the reviewer repository, so it needs no extra token and no read from the reviewed repository.
  - This was chosen over an overlay file inside each reviewed repository. Repository owners cannot be relied on to keep policy correct, and one added precedent can exempt a whole class of findings in effect ("X is protected upstream, not a finding"). With the files in the central repository, no change in a reviewed repository can alter its own review.
  - The cost: [security-team] writes and maintains every overlay, and teams send system context to them instead of editing it themselves. An overlay change is a policy change, so it ships in a MINOR release ([0012](0012-versioned-releases.md)).
- **Validation.** `python -m adaptive_security_reviewer validate-policy` checks the base policy and every overlay in CI.

## Consequences

- Until the reviewer core changes, the test workflow runs the action, so it applies the action's rule set, not the reference one.
- Eval numbers from the POC measure the upstream action with our criteria labels only. They are the baseline that a filled-in policy has to beat.
- The drafts in `policy/base/` were written for the synthetic demo app's architecture. Treat them as a starting list, not as reviewed company policy.

## Future opportunities

- Shared stack packs (for example `policy/packs/aspnetcore.yaml`) that an overlay opts into with `packs: [...]`. Build them only when the same precedent turns up in three or more overlays.
- An agent that proposes precedents from disputed findings and bypass records ([0005](0005-required-check-semantics.md)), for security review.
