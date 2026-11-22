# 0012. Numbered GitHub Releases for the action, gated on the eval

- Status: Proposed
- Date: 2026-11-22
- Related: [0001](0001-composite-action-vs-custom-service.md), [0004](0004-models-and-second-reviewer.md), [0007](0007-eval-corpus-and-judge.md), [0009](0009-rollout-gates.md), [0011](0011-policy-layering.md)

## Context

Every PR in [org] runs whatever version of `adaptive-security-reviewer` the org ruleset points at. A change to the wrapper, the base policy, the model ID or the pinned upstream SHA changes review results across the org. Each of those changes has to be identifiable afterwards: when a finding is disputed or a bug escapes, we need to know which version reviewed the PR. And no change may reach PRs without passing the eval.

The base policy lives in the same repository as the action ([0011](0011-policy-layering.md)), so a policy change is a change to the action.

## Considered options

### Option A: Consumers reference `main`

- Pro: No release process.
- Con: Every merge to `main` goes live org-wide immediately and unversioned. A result cannot be traced back to a version.

### Option B: Semantic-version tags with GitHub Releases, plus a moving major tag (`v1`)

- Pro: Consumers get fixes without editing their reference.
- Con: Moving `v1` changes what runs without a change on the consumer side, which is the same traceability problem as Option A for anyone who pins `v1`.

### Option C: Semantic-version tags with GitHub Releases, consumers pin an exact version

- Pro: What runs on a PR changes only when the ruleset reference changes, and that change is itself reviewed.
- Pro: The release is the unit that carries eval evidence.
- Con: Each release needs a ruleset update to take effect.

## Decision

Option C.

- **Version scheme.** `vMAJOR.MINOR.PATCH`.
  - MAJOR: a breaking change to the action's inputs or outputs, or to the overlay format.
  - MINOR: anything that can change findings, such as a base-policy change, a model change or an upstream SHA bump.
  - PATCH: changes that cannot change findings, such as docs or wrapper fixes with no effect on review output.
- **Release workflow.** `.github/workflows/release.yml` runs on manual dispatch with a version input. It checks that the version is valid semver and higher than the latest release. It runs the policy validator and the full eval with `eval/thresholds.yaml`. Only if the eval passes does it create the tag and the GitHub Release. The release notes contain the generated changelog, the upstream SHA, the model ID, and the eval summary (recall, clean-case false positives, cost per case). The eval report is attached to the release as an asset.
- **Who can release.** Only [security-team] can dispatch the workflow. A tag ruleset on `v*` blocks creating, moving and deleting version tags for everyone except the release workflow.
- **Consumers.** The org-required workflow references the action as `[org]/adaptive-security-reviewer/action@vX.Y.Z`. The org ruleset references that workflow at the same tag. A rollout is a reviewed change of that tag ([0009](0009-rollout-gates.md)).
- **Traceability.** The job summary and the results artifact of every review record the release version.

## Consequences

- Rolling back means pointing the ruleset at the previous tag, with no revert commit needed.
- The release workflow spends one full eval run per release. Its cost per run is in the eval report.

## Open questions

- [decide: whether to turn on GitHub's immutable releases for the repository. Check that the feature is available on [GitHub plan].]
- [decide: whether an org ruleset's required workflow can reference a tag rather than a branch or SHA. Verify against the GitHub documentation before the phase 3 rollout. If it cannot, reference the release's commit SHA and record the version next to it.]

## Future opportunities

- A canary step: point the ruleset at a new release for [pilot repos] for [N] days before org-wide, using the shadow-mode controls from [0009](0009-rollout-gates.md).
