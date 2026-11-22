# 0013. Policy layering: org base policy plus an optional repo overlay read from the base branch

- Status: Proposed
- Date: 2026-11-22
- Related: [0003](0003-classifier-vs-single-policy.md), [0004](0004-precedents-vs-rules.md), [0011](0011-fork-prs-and-untrusted-content.md)

## Context

The review runs on every repository in [org], across different stacks and owners. Some precedents hold everywhere. For example: "environment variables are trusted", "client-side checks are not security", "floating dependency versions are findings". Others belong to one system. For example: "the notes service makes outbound calls only through OutboundClient", or "/internal is restricted by source IP". The security team cannot maintain system-specific knowledge for every repository, and teams cannot be allowed to weaken the org rules for their own code.

The upstream action takes the instructions as two text files per run, `custom-security-scan-instructions` and `false-positive-filtering-instructions`. The filtering file replaces the upstream defaults instead of adding to them.

## Considered options

### Option A: One org policy file, with system-specific precedents in it

- Pro: One place.
- Con: The security team becomes the bottleneck for every system's context, and the file grows with every repository.

### Option B: Per-repo overlay files kept in the central reviewer repo

- Pro: Security controls every change.
- Con: Teams cannot keep their own system's description current, and the central repo grows by one file per repository.

### Option C: Org base policy in the central repo, plus an optional overlay in each repository, read from the PR's base branch

- Pro: Each team maintains its own system description and accepted risks, next to the code they describe.
- Pro: Reading the overlay from the base branch, not the PR head, stops a PR from editing its own policy to pass its own review.
- Con: A team could weaken its overlay in a separate, earlier PR. CODEOWNERS on the overlay path, owned by [security-team], closes that.

## Decision

Option C.

- `policy/base/` in the reviewer repo holds the org policy: global precedents, plus one file per SIA criterion with the criterion's id, name, what it covers, and its precedents. It is stack-agnostic, owned by [security-team], and changed only through PRs that include an eval run.
- `.github/adaptive-security-reviewer.yaml` in a target repository is optional. It may contain `system` (a few lines describing the service), `trust_boundaries`, and `precedents`, each tagged with a criterion id. It cannot remove or override base precedents. Overlay precedents are rendered after the base ones, under the heading "Repository-specific".
- The wrapper reads the overlay with `git show <base-sha>:.github/adaptive-security-reviewer.yaml`. A missing file means no overlay.
- Rendering:
  - **Scan instructions:** the criteria list with the instruction to set `category` to the criterion id, then the system description and trust boundaries, then all precedents.
  - **Filtering instructions:** upstream's default filtering text, then the same precedents.
- The upstream default text is stored in `policy/base/upstream-filter-defaults.txt`. A test compares it with the text produced by upstream's `_generate_single_finding_prompt` at the pinned SHA, and fails after an upstream bump until the copy is updated.
- `python -m adaptive_security_reviewer validate-policy` checks both layers, and runs in CI for the reviewer repo and for any repo that has an overlay.

## Consequences

- Most repositories start with no overlay and get only the base policy. Precision on those repos is expected to be lower than on repos with an overlay. Shadow-mode data ([0010](0010-rollout-gates.md)) shows which repos need one first.
- The demo app in this POC has an overlay, which shows the format.

## Future opportunities

- Shared stack packs (for example `policy/packs/aspnetcore.yaml`, `policy/packs/kubernetes.yaml`) that an overlay opts into with `packs: [...]`. Build them only when the same precedent turns up in three or more overlays.
- An agent that proposes overlay precedents from disputed findings and author replies, for team and security review.
