### **Starting a new project** regarding the [AI-native SDLC playbook](https://claude.com/resources/articles/the-ai-native-sdlc-playbook).

1. **Set up an intent home.** In the product repo, create an `intent/` folder, agree on an `intent.md` template, and decide who can write to it.
2. **Write the intent.** Describe the problem to Claude in your own words and let it ask about scope, users, constraints, and success criteria. Have it produce `intent.md` (problem, proposed outcome, affected users and systems, constraints, open questions), correct it, and commit it.
3. **Generate the spec.** Load your organization's skills (brand, security, compliance, UX) and have Claude turn `intent.md` into `spec.md`, flagging concerns. Resolve the flags with policy owners, then commit `spec.md` beside `intent.md`.
4. **Prepare the repo for agents.** Run `/init` to generate `CLAUDE.md`, trim it to what a new joiner needs, add the build, test, and lint commands, and commit it at the root.
5. **Add guardrails.** Put recurring policies in skills (`.claude/skills/<name>/SKILL.md`). Enforce non-negotiable rules with hooks, such as blocking edits to protected paths.

**Artifacts each stage commits**

- `intent.md`: the originator's idea, captured once.
- `spec.md`: requirements and design, reviewed by the product owner.
- `plan.md`: the plan from Claude Code plan mode, approved before implementation and updated when the work diverges.
- `CLAUDE.md`: conventions, commands, and recurring mistakes, read at the start of every session.
- Skills and hooks: policy as guidance (skills) and as enforcement (hooks).

**Stages:** Plan → Design → Build → Test → Deploy → Maintain. Build starts in plan mode. Test requires a verification command (tests, build, lint) to pass before Claude reports done. Deploy adds AI code review against `REVIEW.md`, plus branch protection and hook-gated production deploys.

The entry points with no prerequisites are capturing intent, creating `CLAUDE.md`, setting up the feedback loop, and writing skills.

If you want, I can scaffold this in a new folder: an `intent/` template, a starter `CLAUDE.md`, and a `plan.md` placeholder.

Sources:
- [The AI-native SDLC playbook (claude.com)](https://claude.com/resources/articles/the-ai-native-sdlc-playbook)
- [The AI-native SDLC playbook (blog version)](https://claude.com/blog/the-ai-native-sdlc-playbook)
- [Claude Academy course](https://academy.claude.com/courses/ai-native-sdlc-playbook)


## AI-native SDLC playbook: steps

Sources: `AI-SDLC-Playbook-howto.md` (step list) and `AI-SDLC-Start.png` (building blocks). Not yet checked against the published article.

## Part 1: Starting a new project (in order)

| # | Step | What you do | Output committed |
|---|---|---|---|
| 1 | Set up intent home | Create an `intent/` folder, agree on an `intent.md` template, decide who can write to it | `intent/` folder |
| 2 | Write the intent | Describe the problem to Claude; it asks about scope, users, constraints, success criteria | `intent.md` |
| 3 | Generate the spec | Load org skills (brand, security, compliance, UX); Claude turns intent into spec and flags concerns; resolve flags with policy owners | `spec.md` |
| 4 | Prepare repo for agents | Run `/init`, trim `CLAUDE.md` to what a new joiner needs, add build/test/lint commands | `CLAUDE.md` at root |
| 5 | Add guardrails | Recurring policies go in skills; non-negotiable rules go in hooks (e.g., block edits to protected paths) | `.claude/skills/<name>/SKILL.md`, hook config |

## Part 2: Lifecycle stages

| Stage | What happens | Key rule |
|---|---|---|
| Plan | Capture intent | Plan approved before implementation |
| Design | Requirements and design | Spec reviewed by product owner |
| Build | Implement with Claude | Starts in plan mode; `plan.md` updated when work diverges |
| Test | Verify the work | A verification command (tests, build, lint) must pass before Claude reports done |
| Deploy | AI code review, CI/CD, PR review | Review against `REVIEW.md`; branch protection; hook-gated production deploys |
| Maintain | Close the loop | Feedback feeds back into intent and skills |

## Part 3: Building blocks (from the diagram)

| Block | Stage | Dependency |
|---|---|---|
| Capture intent | Plan | None (start here) |
| CLAUDE.md | Build | None |
| Skills | Build | Helps subagents and PR review; not required |
| Feedback loop | Test | Helps subagents; feeds evals |
| Hooks | Deploy | None |
| Plan mode | Build | None |
| Requirements & design | Design | Needs intent + skills |
| Subagents | Build | Needs CLAUDE.md, helped by skills |
| Evals | Test | Needs CLAUDE.md and feedback loop |
| PR review | Deploy | Needs subagents, evals, skills |
| CI/CD | Deploy | Needs PR review and hooks |
| Closing the loop | Maintain | Needs intent and CI/CD |

Solid arrows in the diagram mean "adopt first"; dotted arrows mean "helps, not required". The six blocks with no prerequisites are capture intent, CLAUDE.md, skills, feedback loop, hooks, and plan mode.
