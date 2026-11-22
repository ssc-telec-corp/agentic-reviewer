# 0008. Eval corpus of labelled commits, run through the upstream pipeline, judged by an LLM

- Status: Proposed
- Date: 2026-11-22
- Related: [0002](0002-composite-action-vs-custom-service.md), [0004](0004-precedents-vs-rules.md), [0005](0005-models-and-second-reviewer.md), [0009](0009-measuring-precision.md)

## Context

Policy, model and upstream-version changes alter review quality in ways that cannot be predicted from the diff. Regressions need a labelled set of changes with known outcomes that can be rerun on a schedule. The POC builds the corpus from synthetic changes against a demo app. Production will use real vulnerabilities found by bug bounty, incidents and audits, each traced to the commit that introduced it, with the same schema and harness.

The upstream action gets its PR data from the GitHub API, and has its own eval tool (`claudecode/evals/`) that runs on one GitHub PR and prints findings without grading them. Corpus cases are commits, not PRs.

A reviewer can describe a real bug in different words, cite a different line, or split it into two findings, so exact file and line matching undercounts recall.

## Considered options

### Option A: Open a GitHub PR per case and run the real action

- Pro: Exactly the production path.
- Con: Real company cases would need throwaway PRs in production repos. A run cannot happen offline, and its results are not in one place.

### Option B: Run upstream `main()` locally, with PR data built from git

- Pro: Same prompt, Claude Code call, regex exclusions, filter and output as the action, at the same pinned SHA. Only the two GitHub fetch methods are replaced.
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
- **Thresholds.** `eval/thresholds.yaml` sets the minimum recall and the maximum clean-case false positives for the gate model. The scheduled workflow fails when either is crossed.

## Consequences

- With one run per case per model, nondeterminism shows up as noise between runs. The thresholds leave margin for it.
- Judge decisions are saved with each result, for audit.

## Future opportunities

- Three runs per case to measure variance, once the per-case cost is known.
- Run a headless-variant reviewer next to the upstream pipeline in the same report ([0002](0002-composite-action-vs-custom-service.md)).
- Automatic miss triage that reads the Claude Code transcript (file not read, read but not flagged), as the reference design does.
- [decide: how often judge decisions are audited by hand, and by whom.]
