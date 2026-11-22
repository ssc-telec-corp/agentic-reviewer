# adaptive-security-reviewer

Every pull request in [org] gets an automated Security Impact Assessment (SIA). It replaces the SIA ticket and the manual security review. Engineers do nothing extra: the review starts when the PR is opened, posts its findings as PR comments, and reports one required check.

## What happens on a pull request

```mermaid
flowchart TD
    A["Engineer opens or updates a PR"] --> B["Org ruleset starts the required check"]
    B --> C

    subgraph W["adaptive-security-reviewer vX.Y.Z (our wrapper)"]
        C["Load the policy:<br/>org base rules + this repo's overlay"]
        subgraph U["claude-code-security-review (Anthropic's action, unchanged)"]
            D1["1. Claude reads the change<br/>and lists possible vulnerabilities"]
            D2["2. Known noise is dropped"]
            D3["3. Claude double-checks each one<br/>against our policy"]
            D1 --> D2 --> D3
        end
        C --> D1
        D3 --> E{"Did the review<br/>complete?"}
        H{"Any HIGH severity<br/>finding?"}
    end

    E -- "no: API outage or error" --> G["Check fails<br/>(on-call can switch it<br/>to pass with a warning)"]
    E -- "yes" --> H
    H -- "no" --> F["Findings posted as PR comments<br/>check passes, PR can merge"]
    H -- "yes" --> B2["Check fails, PR cannot merge"]
    B2 --> I["Engineer fixes the code and pushes<br/>(the review runs again)"]
    B2 --> J["Finding is wrong:<br/>security team reviews<br/>and merges through a bypass"]
```

A PR cannot merge while the review reports a HIGH severity finding, or while the review cannot run. MEDIUM and LOW findings are comments only. When a HIGH finding is wrong, the security team decides and merges through a recorded bypass. Blocking on HIGH findings is switched on during rollout, once the security team has measured how often HIGH findings are right.

## How the review stays good

```mermaid
flowchart TD
    P["Policy change or new model"] --> E["Eval: run the reviewer on known vulnerable<br/>and known clean changes"]
    C["New eval case"] --> E
    S["Weekly schedule in GitHub Actions<br/>(catches changes in Claude Code and models)"] --> E
    E --> T{"Recall and false positives<br/>within thresholds?"}
    T -- "no" --> X["Run fails, security team alerted"]
    X --> P
    T -- "yes" --> R["Numbered release vX.Y.Z"]
    R --> O["Org ruleset points at the new release"]
    O --> PR["Reviews on every PR"]
    PR -- "finding disputed by an engineer" --> P
    PR -- "bug found later (bounty, incident, audit)" --> C
```

Each missed bug becomes a test case, and each wrong finding can become a policy fix. A change reaches PRs only as a numbered release that passed the eval. The weekly run catches quality changes that come from outside the repository, such as a new Claude Code version.

## Documents

- [design.md](design.md): what is built and how it fits together
- [policy-authoring.md](policy-authoring.md): how to add or change a precedent, and how a repository adds its overlay
- [evaluation.md](evaluation.md): corpus format, judge, thresholds, how to add a case
- [runbook.md](runbook.md): outages, bypass, cost alerts, releases, contacts
- [engineer-faq.md](engineer-faq.md): for PR authors, covering what the bot does and how to respond
- [adr/](adr/README.md): the decisions behind the design, and why
