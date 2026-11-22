# 0007. Eval corpus of labelled commits, run through the reviewer core, judged by an LLM

- Status: Proposed
- Date: 2026-11-23
- Related: [0001](0001-reviewer-core.md), [0003](0003-precedents-vs-rules.md), [0004](0004-models-and-second-reviewer.md), [0008](0008-measuring-precision.md), [0010](0010-local-vs-ci-authentication.md), [0011](0011-policy-layering.md), [0012](0012-versioned-releases.md)

## Context

Policy, model and upstream-version changes alter review quality in ways that cannot be predicted from the diff. Regressions need a labelled set of changes with known outcomes that can be rerun on a schedule. The proof of concept (POC) builds the corpus from synthetic changes against a demo app. Production will use real vulnerabilities found by bug bounty, incidents and audits, each traced to the commit that introduced it, with the same schema and harness.

The upstream action gets its PR data from the GitHub API, and has its own eval tool (`claudecode/evals/`) that runs on one GitHub PR and prints findings without grading them. Corpus cases are commits, not PRs.

A reviewer can describe a real bug in different words, cite a different line, or split it into two findings, so exact file and line matching undercounts recall.

## Considered options

### Option A: Open a GitHub PR per case and run the real action

- Pro: Exactly the production path.
- Con: Real company cases would need throwaway PRs in production repos. A run cannot happen offline, and its results are not in one place.

### Option B: Run upstream `main()` locally, with PR data built from git

- Pro: Same prompt, Claude Code call, regex exclusions, filter and output as the action, at the same pinned SHA. The replacement for the GitHub fetch must also reproduce what that fetch does besides fetching: it drops generated files, keeps only the first 100 files, and requires `GITHUB_TOKEN` to be set. Otherwise the eval measures a different pipeline.
- Note: this option assumes the upstream code is the reviewer core. If [0001](0001-reviewer-core.md) chooses a headless reviewer, the harness calls it directly with a code tree and a diff instead.
- Con: It does not exercise the comment script or the action's shell steps. The CI PR runs cover those.

### Matching: exact vs LLM judge

- Exact matching fails when the reviewer anchors the finding at the caller instead of the callee, which is the normal outcome for multi-file cases.
- An LLM judge matches by meaning, at a cost of one call per vulnerable case and some leniency risk.

## Decision

Option B, with an LLM judge for vulnerable cases.

- **Case format.** One YAML file per case in `eval/corpus/cases/`. It records the repo, the reviewed path, base and head refs, PR title and body, `expect: finding|none`, `difficulty: standard|hard`, the criterion, and for vulnerable cases an `expected_finding` with files and a description. `eval/corpus/convert.py` turns a commit into a case file.
- **Isolation.** The harness exports only the reviewed path at the head ref into a temporary directory. Claude Code runs there, so it cannot read the corpus, the policy or other branches.
- **Judge.** `claude-sonnet-5-5`, with no tools. It sees the expected description, the kept findings and the excluded findings. It reports which kept finding matches, if any, and which excluded finding matches, if any.
- **Scoring.** A vulnerable case is caught if a kept finding matches. Every kept finding on a clean case counts as one false positive. Misses are classified as dropped by hard rules, dropped by the filter, or never reported.
- **Thresholds.** `eval/thresholds.yaml` sets the minimum recall and the maximum clean-case false positives for the gate model, the model the review uses in production ([0004](0004-models-and-second-reviewer.md)). Any run that crosses either one fails.

## Where the eval runs

The same command runs in three places, with no change to the harness:

- **Locally**, for a developer changing the policy or the wrapper. One command runs the whole corpus and writes the report.
- **On a schedule in GitHub Actions**, using the workflow `.github/workflows/adaptive-security-reviewer-eval.yml`. It runs every [Monday 06:00 UTC] and on manual dispatch. A scheduled run is needed even when nothing in this repository changed, because three things outside it change review quality:
  - Upstream installs the latest Claude Code from npm on every run, with no version pin ([0001](0001-reviewer-core.md)).
  - Model behaviour behind a fixed model ID can change.
  - The filter's hard-coded validation model can be retired, which silently turns filtering off ([0010](0010-local-vs-ci-authentication.md)).
- **In the release workflow**, as the gate before a version is tagged ([0012](0012-versioned-releases.md)).

The scheduled workflow:
- checks out the repository with full history and fetches every case's refs;
- runs the corpus on both models ([0004](0004-models-and-second-reviewer.md));
- writes the report to the job summary;
- uploads the full report, with per-case results and judge decisions, as an artifact kept for [90] days;
- compares the numbers with the eval report attached to the latest release, and shows the difference per case.

The workflow fails when the gate model crosses `eval/thresholds.yaml`. A failure notifies [Adaptive Security team channel]. It runs one at a time (one concurrency group), with read-only repository permissions and the `ANTHROPIC_API_KEY` secret.

Checks that do not call a model run on every PR to this repository in a separate, cheap workflow:
- the policy validator;
- the test that compares the upstream filter defaults with the pinned SHA ([0011](0011-policy-layering.md));
- validation of every corpus case file;
- the harness's unit tests;
- the docs lint.

## Consequences

- With one run per case per model, nondeterminism shows up as noise between runs. The thresholds leave margin for it, and the comparison with the last release shows which cases flipped.
- Judge decisions are saved with each result, for audit.
- Every scheduled run costs one full eval. The cost per run is in the report. Frequency is the lever if it is too high.
- Real company cases live in other repositories. The scheduled workflow needs read access to them, through a GitHub App token or deploy keys, and the synthetic corpus does not.

## Open questions

- [decide: how cost per case is measured. The upstream action records no cost ([0001](0001-reviewer-core.md), problem 9), so the harness has to collect it itself, from Claude Code's reported cost plus the token counts of any filter calls.]

- [decide: schedule frequency (weekly proposed) and the monthly eval budget [budget].]
- [decide: how CI gets read access to the repositories that real cases come from.]
- [decide: how often judge decisions are audited by hand, and by whom.]

## Future opportunities

- Three runs per case to measure variance, once the per-case cost is known.
- Run a headless-variant reviewer next to the upstream pipeline in the same report ([0001](0001-reviewer-core.md)).
- Automatic miss triage that reads the Claude Code transcript (file not read, read but not flagged), as the reference design does.
