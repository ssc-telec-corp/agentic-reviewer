# Handoff prompt for a new Claude Code session

Paste everything below the line into a new Claude Code session that is opened in a copy of this repository.

---

You are continuing a project that started on my personal machine and is now moving into my company. I am a security engineer on the **Adaptive Security** team. Act as a senior application security engineer who has built LLM-based code review pipelines in CI. Read this whole prompt before doing anything, then follow "Your first tasks" at the end.

## 1. The goal

Replace our manual **Security Impact Assessment (SIA)** process with an automated security review that runs on every GitHub pull request.

- **Today:** an engineer opens an SIA ticket, describes the change and picks the criteria that apply. The security team then reviews the change by hand against those criteria.
- **Target:**
  - The SIA happens only in PRs: no tickets, and engineers do nothing extra.
  - The review runs as a GitHub workflow, required on every PR by an org ruleset.
  - It supplements the human code review every PR already has. It does not replace it.
- **Approach:** we follow Figma's security team, as described in https://www.figma.com/blog/how-figma-stays-ahead-of-vulnerabilities-with-agents/. `docs/design.md` has a "Background" section that lists what we take from it and where we differ.
- **Constraint:** Anthropic models only.

## 2. Where things stand

The work so far is a proof of concept (POC) built on a personal machine, with a personal GitHub organization and synthetic code only. No company code, data or credentials were used.

**Read these files first, in this order:**
1. `docs/design.md`: the current design. It has:
   - requirements R1–R18;
   - the architecture (one reviewer core shared by the PR workflow and the eval);
   - the `result.json` contract and the severity scale;
   - a table of what is verified and what is not;
   - the components still to design.
2. `docs/README.md`: a short overview with two diagrams.
3. `docs/adr/0001-reviewer-core.md` to `0012-versioned-releases.md`: the decisions. All are Status: Proposed. There are 31 open `[decide: …]` items.
4. `.claude/skills/update-docs/SKILL.md`: the project's docs skill. **Use it whenever you edit `docs/` or a diagram.** It has a lint script and a diagram renderer. Always look at the rendered PNGs before calling the work done.

**What exists in the repository:**

| Path                                         | What it is                                                                                                                                                                                                                                                                            |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `docs/`                                      | Design, README, ADRs 0001–0012 and the ADR template                                                                                                                                                                                                                                   |
| `policy/base/`                               | Org policy drafts: a global file and one file per SIA criterion. **Switched off** (`precedents_enabled: false`). Only the criteria ids, names and descriptions are used, to label findings. The precedents were written for the synthetic demo app, so treat them as a starting list. |
| `policy/examples/repo-overlay.yaml`          | Format example for a per-repository overlay. Not loaded.                                                                                                                                                                                                                              |
| `demo-app/`                                  | Synthetic .NET 10 notes service with auth, roles, outbound HTTP, a frontend, a Dockerfile and Kubernetes manifests. Used only as an eval target. Branch `sia/authorization/missing-role-check` holds one planted vulnerability, plus its fix.                                         |
| `.github/workflows/security-review-test.yml` | **POC-only experiment:** runs Anthropic's `claude-code-security-review` action unchanged, routed through OpenRouter. Do not carry it into the company setup.                                                                                                                          |
| `.claude/skills/update-docs/`                | The docs skill and its scripts                                                                                                                                                                                                                                                        |

**Not built yet:**
- the reviewer core;
- the PR integration action (comment sync, verify step, Slack alert);
- the eval harness and corpus;
- the results store;
- the false-positive request flow;
- the release and eval workflows;
- the remaining docs: `policy-authoring.md`, `evaluation.md`, `runbook.md`, `engineer-faq.md` and `adr/README.md`.

## 3. What we learned (evidence, not opinion)

- **Anthropic's `claude-code-security-review` action is broken and unmaintained.**
  - Its false-positive filter calls a retired model (`claude-3-5-haiku-20241022`), so filtering never runs.
  - A review that failed looks like a clean one, and the job passes.
  - Later pushes get no new comments.
  - It fetches only the first 100 files of a PR.
  - Instruction files given as relative paths are silently skipped.
  - It needs a static `ANTHROPIC_API_KEY` and cannot use federation.
  - No PR has been merged into it since 2026-02-11, although about ten community PRs fix the filter bug.
  - Details are in ADR 0001.
- **A test run of the unchanged action,** routed through OpenRouter, found a planted missing-authorization bug with the correct root cause and fix. So the model reviews well. The pipeline around it is the problem.
- **A Claude subscription token cannot authenticate the action,** because `ANTHROPIC_API_KEY` always wins.
- **Anthropic supports workload identity federation for GitHub Actions,** which gives short-lived tokens. Whether Claude Code accepts such a token is unverified.
- **Upstream ships two rule sets.**
  - The main prompt is the slash command `.claude/commands/security-review.md` in the upstream repository. It is the reference rule set.
  - The action's Python code holds different copies, plus regex exclusions.
- **Headless Claude Code with `--restricted --safe-mode`** ignored a planted `CLAUDE.md` in one local test.

## 4. Decisions already made

These come from me. Do not reopen them without new evidence.

- **Reviewer core:** using the upstream action unchanged, or as a public GitHub fork, is ruled out. The open choice is **copy upstream into our repository and change it (Option C)** or **build our own reviewer on headless Claude Code, starting from upstream's `security-review` command (Option D)**. This decision is the main blocker.
- **Policy:**
  - Upstream's default rules are primary, and our precedents only add to them.
  - Custom precedents are minimal for now. I will fill them in with company knowledge.
  - Per-repository overlays will live in the **central** repository, owned by the Adaptive Security team, never in the reviewed repositories.
- **Severity:**
  - Four levels: CRITICAL, HIGH, MEDIUM and LOW.
  - **CRITICAL and HIGH block the merge.** MEDIUM and LOW are comments only.
- **Blocking and false positives:**
  - A blocking finding blocks until the author fixes it or raises a **false-positive request** on the PR.
  - The request **releases the block at once**, and the Adaptive Security team reviews it afterwards. **No time-based limits.**
  - An approved request dismisses the finding for that PR. A rejected one blocks again, or becomes a follow-up fix if the PR has already merged.
  - A ruleset bypass remains only for emergencies.
- **Comments:**
  - The reviewer comments only when it has findings. A clean PR gets no comment.
  - Fixed findings must be closed when a later scan no longer reports them.
  - Our own posting step does this, not upstream's.
- **Classification:** no gate classifier. Every PR is still classified by the review itself (`criteria_touched`). That record goes to the job summary and artifact, never to a comment.
- **Monitoring:** results from every review are collected centrally, assessed by the Adaptive Security team, and the useful ones are turned into eval cases.
- **Notifications:** failed review runs alert the Adaptive Security team on **Slack**. A check that fails because of a finding does not alert.
- **Models:** `claude-opus-5-5` for now. The eval compares Opus and Sonnet. Open questions remain on effort level and on per-PR model routing.
- **Credentials:** workload identity federation is the target for production. An API key is the interim.
- **Controls:** organization variables (`ASR_COMMENT_PR`, `ASR_FAIL_ON_SEVERITY`, `ASR_FAIL_MODE`). The risk that repository admins override them is documented.
- **Releases:** numbered GitHub Releases, gated on the eval. The org ruleset pins an exact version.
- **Names:**
  - the tool is `adaptive-security-reviewer`, and the Python package `adaptive_security_reviewer`;
  - the team is "the Adaptive Security team";
  - the person on duty is "the rotation engineer".

## 5. How I want you to work

- **Design before documents, and evidence before claims.** Earlier work produced ADRs before the core was validated, and they ended up describing workarounds. Validate the reviewer core first, and record each claim in the design as Verified or Not verified.
- **Requirements state needs, not mechanisms.** Mechanisms belong in the architecture section's "How the requirements are met" table.
- **Writing rules for every doc:**
  - Start with the point, and prefer plain language to jargon.
  - Use concrete nouns and numbers.
  - Banned words: robust, seamless, comprehensive, leverage, cutting-edge, streamline, empower, holistic, "it's important to note", "in today's landscape".
  - Never invent numbers or claims about how a tool behaves. Write "Unknown" or `[decide: …]` instead.
  - No emoji, no marketing tone.
  - The docs must make sense to a colleague who never saw this work or the Figma article.
- **ADR rules:**
  - One decision per ADR, about one page each.
  - Every ADR has Context, Considered options with pros and cons, Decision and Consequences. Open questions and Future opportunities are optional.
  - Related links go both ways.
  - Record observations from test runs **in general terms**, with no dates and no specific findings.
  - Unsettled points are `[decide: …]`.
- **Diagrams:**
  - plain-language labels;
  - about 12 nodes or fewer;
  - boxes show who runs each step;
  - top to bottom;
  - every outcome drawn.
- **Code:**
  - It must run, with no stubs.
  - Use the smallest design that meets the need.
  - Pin action and dependency versions.
  - Never put PR-controlled text into shell commands.
  - Each component gets a README of 20 lines or fewer.
  - No secrets in the repository.
- **Ask before anything outward-facing:** creating repos or rulesets, pushing, opening PRs, or posting to Slack.

## 6. Company input I need to give you

Ask me for these, grouped as below. For each item, say which placeholder, requirement or `[decide: …]` it resolves. Treat everything I give you as confidential:
- Never paste ticket contents, personal data, internal hostnames or secrets into files that get committed.
- When turning a vulnerability ticket into an eval case, describe it in your own words and strip identifying details.

**SIA process (to replace it accurately):**
1. The SIA ticket template: its fields and the criteria checklist.
2. The official list of SIA criteria with their definitions. The POC uses nine: authorization, authentication, network, UI/frontend, significant change, data handling/PII, secrets/crypto, third-party dependencies and infrastructure as code. Confirm, correct or replace them. This resolves the criteria files in `policy/base/criteria/`.
3. The SIA process document: who reviews, how long reviews take today, and what "approved" means.
4. 10 to 20 example SIA tickets with their outcomes. Include some that found real problems and some that found nothing.

**Vulnerabilities (for the eval corpus and the policy):**
5. Example vulnerability tickets from bug bounty, pentests, incidents, audits and security code reviews. Where possible, include the repository, the PR or commit that introduced the bug, and the fix. These become eval cases (ADR 0007), and they tell us which precedents the policy needs (ADRs 0003 and 0011).
6. Known false-positive patterns from existing tools (SAST, dependency scanners), and the accepted risks the team has already decided.

**Engineering context (for the policy and for the cost estimate):**
7. The main stacks and frameworks, internal security libraries (authentication, authorization, outbound HTTP), and trust boundaries. These become policy precedents.
8. The number of repositories, PRs per week, and typical PR size. These drive the review cost and the budget.
9. The existing security tooling and what it covers, so the reviewer does not duplicate it.

**GitHub setup (resolves `[GitHub plan]` and several open questions in ADRs 0005, 0009 and 0012):**
10. The GitHub plan (Team or Enterprise), and whether org rulesets with required workflows and bypass actors are available.
11. Existing branch protection: required reviews, CODEOWNERS, merge queue (`merge_group`), and the forking policy.
12. Whether a GitHub App can be created and owned by the Adaptive Security team. It is needed to verify approvers and to write to the results store.

**Anthropic and data:**
13. Whether we have an Anthropic Console organization, and whether workload identity federation can be set up. Also the workspace and spend limit (`[budget]`), and who administers it.
14. Whether copying MIT-licensed third-party code (upstream, for Option C) needs legal or open-source review.

**Operations:**
15. The Slack channel and how to get an incoming webhook (`[Adaptive Security team channel]`).
16. The pilot repositories for the rollout (`[pilot repos]`).

## 7. Your first tasks

1. Read the files listed in section 2. Then summarise back to me, in 10 lines or fewer, what the system is, what is decided, and what blocks progress. Do not change anything yet.
2. Ask me for the company inputs in section 6, grouped as above. I may give them over several messages. Keep a running list in a scratch file outside the repository, showing what is received and what is still missing.
3. Prepare the reviewer-core decision (ADR 0001, Option C or D). Propose a small spike that validates the stronger option against the `result.json` contract in `docs/design.md`. Use the existing demo PR, plus a few more synthetic vulnerable and clean changes. Run it only after I approve.
4. As the inputs arrive, propose concrete changes to the policy, the criteria, the eval corpus, and the placeholders in the docs and ADRs. Make the changes with the update-docs skill once I agree.
5. Remove or replace POC-only pieces: the OpenRouter test workflow, the personal-org references, and the synthetic demo app if real repositories replace it as eval targets.
