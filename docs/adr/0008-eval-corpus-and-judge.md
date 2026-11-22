# 0008. Eval corpus of labelled commits, an LLM judge for matching, deterministic scoring for clean cases

- Status: Proposed
- Date: 2026-11-22
- Related: [0004](0004-precedents-vs-rules.md), [0005](0005-models-and-second-reviewer.md), [0009](0009-measuring-precision.md)

## Context

Policy and model changes alter review quality in ways that cannot be predicted from reading the diff. To catch regressions, we need a labelled set of changes with known outcomes that can be re-run on a schedule. The POC builds the corpus from synthetic changes against a demo app. In production, the corpus will be built from real vulnerabilities found by bug bounty, incidents and audits, each traced to the commit that introduced it. Both kinds of case must use one schema and one harness.

A reviewer can describe a real bug in different words, cite a different line, or split it into two findings. Matching findings against labels by exact file and line undercounts recall.

## Considered options

### Option A: Exact matching on file and line

- Pro: Deterministic and free.
- Con: It fails whenever the reviewer anchors the finding to the caller instead of the callee. That is the normal outcome for the multi-file cases this corpus is meant to test.

### Option B: An LLM judge compares each expected finding with the reviewer's findings

- Pro: It matches by meaning: same root cause and same impact, whatever the wording.
- Con: The judge can be lenient or strict, and it costs a model call per vulnerable case.

### Option C: Human grading of every run

- Pro: Most accurate.
- Con: Cannot run on a schedule.

## Decision

Option B for vulnerable cases. Clean cases are scored without a judge.

- **Case format.** One YAML file per case in `eval/corpus/cases/`. It records the repo, the reviewed path, base and head refs, PR title and body, `expect: finding|none`, `difficulty: standard|hard`, the primary criterion, the expected `criteria_touched`, and for vulnerable cases an `expected_finding` with files and a description. `eval/corpus/convert.py` turns a real commit into a case file, so adding a case means adding one file.
- **Isolation.** The harness exports only the reviewed path at the head ref (`git archive`) into a temporary directory and computes the diff relative to that path. The reviewer cannot read the corpus, the policy files or other branches.
- **Judge.** `claude-sonnet-5-5`, with no tools. It receives the expected description, the reviewer's reported findings and its dropped candidates. It returns which finding matches, if any, and which dropped candidate matches, if any. The judge model is pinned and is not changed in the same PR as a reviewer model change.
- **Scoring.** A vulnerable case is caught if a reported finding matches. A clean case with any reported finding counts one false positive per finding. Each miss is classified from the reviewer's transcript: the reviewer dropped it, the adjudicator dismissed it, the file was read but not flagged, or the file was only seen in the diff.
- **Thresholds.** `eval/thresholds.yaml` sets the minimum recall and the maximum clean-case false positives for the gate model. The scheduled workflow fails when either is crossed.

## Consequences

- One run per case per model means model nondeterminism shows up as noise between runs. The report gives single-run numbers, and the thresholds leave a margin for it ([open question](#open-questions)).
- The judge's decisions are saved with each result, so a human can audit a match.

## Open questions

- [decide: number of runs per case. One run per case keeps cost down, and three would show variance. Needs the per-case cost from the POC report.]
- [decide: how often to audit judge decisions by hand, and who does it.]
