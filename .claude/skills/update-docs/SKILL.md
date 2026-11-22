---
name: update-docs
description: Update the adaptive-security-reviewer documentation in docs/ (README, design.md, policy-authoring, evaluation, runbook, engineer FAQ, ADRs) and especially its Mermaid diagrams, keeping them true to the code and readable. Use this whenever docs/ or an ADR is created or edited, whenever a diagram is added or changed, and also after any change to action/, policy/, eval/ or .github/workflows/ that alters behaviour the docs describe, even if the user did not mention the docs.
---

# Updating the docs

The docs are read by people at the company who never saw how this system was built: security engineers who operate it, engineers whose PRs it reviews, and platform owners who maintain the org ruleset. Every sentence has to be true of what is built and useful to one of them. A diagram that disagrees with the code is worse than no diagram, because readers trust pictures more than prose.

## Workflow

1. **Find the source of truth before writing.** Docs describe what exists, so read the code or config the text is about: `action/action.yml` for steps and inputs, `.github/workflows/*.yml` for triggers and permissions, `policy/` for the policy format, `eval/` for the corpus and thresholds, and the ADRs for reasons. Where something is not built or not measured, write "Unknown" or `[decide: ...]` instead of guessing a number or a behaviour.
2. **Find every place the change shows up.** `grep -rn` the changed name, input, variable or step across `docs/`. Diagrams repeat facts from the prose (the PR-flow diagram in `docs/README.md` is reused in `docs/design.md`), so a renamed step usually needs more than one edit.
3. **Edit.** Follow the writing rules and diagram rules below.
4. **Check.** From the repo root:
   - `python3 .claude/skills/update-docs/scripts/lint_docs.py` checks for banned words, emoji, stub markers, the required ADR sections and broken ADR links.
   - `python3 .claude/skills/update-docs/scripts/render_diagrams.py [files]` renders every Mermaid block to PNG and prints the paths.
5. **Look at every rendered PNG** with the Read tool before calling the work done. A diagram can render without errors and still mislead, so look for the problems listed under "Diagram review" and fix any you find, then render again.
6. **Report** which files changed, which diagrams you re-rendered, and anything you marked Unknown or `[decide: ...]`.

## Writing rules

These come from the people who own this repo. The reason behind most of them: readers are busy, and anything generic makes them skim past the parts that matter.

- Start with the point. No preamble, no restating the question, no closing summary.
- Use concrete nouns and numbers instead of adjectives. Banned words: robust, seamless, comprehensive, leverage, cutting-edge, streamline, empower, holistic, "it's important to note", "in today's landscape". The lint script catches these.
- Cut any sentence that would be just as true of another project. Give no generic security advice, only what applies to this system.
- Use a table or bullets only when the content really is a list. Otherwise write prose.
- Never invent numbers, benchmarks or tool behaviour. Numbers come from the eval report, and tool behaviour from the source or a test run.
- Use active voice. No emoji, no marketing tone.
- Use `[placeholders]` for company-specific values (`[org]`, `[security-team]`, `[budget]`), and never refer to anyone's personal setup or to the conversation the docs came from. The docs get copied into the company as they are.

## ADR rules

- Use `docs/adr/template.md`: Status, Date, Related, then Context, Considered options (pros and cons for each), Decision, Consequences, and optionally Open questions and Future opportunities.
- Each ADR covers one decision and fits on about one page. New ADRs start as `Proposed`.
- When a decision changes, add a new ADR that supersedes the old one and set the old one's status to `Superseded by NNNN`. Do not rewrite the history, because the reasons for rejecting an option are the useful part.
- Link related ADRs both ways, and add every new ADR to `docs/adr/README.md` with its status.
- Write unsettled points as `[decide: ...]`, together with the data needed to settle them, rather than quietly picking an option.

## Diagram rules

Diagrams exist so that someone new understands the system in one minute. Optimize for that reader, not for completeness.

- **Plain-language labels.** Write "Claude double-checks each finding against our policy", not `FindingsFilter.filter_findings`. Code names belong in `design.md` prose. Put a version or name in a label only when the reader needs it, as with `vX.Y.Z` on the reviewer.
- **About 12 nodes or fewer per diagram.** If it needs more, split it into two diagrams, each answering one question, as the README does with "what happens on a PR" and "how the review stays good".
- **Boundaries mean ownership.** A `subgraph` shows who owns or runs a step: our wrapper, Anthropic's unchanged action, GitHub, the engineer. Every node must sit inside the boundary that actually runs it. For example, the org ruleset is GitHub's, so it sits outside the wrapper. Nest a subgraph when one component runs inside another.
- **Top-down (`flowchart TD`)** by default. Long left-to-right chains with feedback loops render wide, and their arrows cross.
- **Label decision edges** with the condition ("yes", "no: API outage or error"), and make sure every outcome the code has gets an edge. If the check can fail, the diagram shows it failing.
- **Use quoted labels** (`A["text"]`) and `<br/>` for line breaks. Parentheses, colons and slashes inside unquoted labels break the Mermaid parser.
- Follow each diagram with one or two sentences that state the conclusion the reader should take away, such as "Findings never block the merge."

## Diagram review

After rendering, check the PNG for:

- nodes drawn inside the wrong boundary;
- arrows that cross or loop around the whole picture (reorder the nodes, or switch to TD);
- labels cut into fragments of one or two words (shorten them or add `<br/>`);
- missing outcomes, such as a decision with only a "yes" edge;
- a step that has no counterpart in `action/action.yml` or the workflows, or a step in the code that the diagram leaves out where a reader would need it.
