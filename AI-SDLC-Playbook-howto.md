I took "ai-sdlc playbook" to mean Anthropic's [AI-native SDLC playbook](https://claude.com/resources/articles/the-ai-native-sdlc-playbook). Nothing in this repo references it, so I haven't created any files.

**Starting a new project**

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

If you want, I can scaffold this in a new folder: an `intent/` template, a starter `CLAUDE.md`, and a `plan.md` placeholder. Tell me the project name and stack, or whether it goes in this repo (the ASP.NET MVC 5 project) or a new one.

Sources:
- [The AI-native SDLC playbook (claude.com)](https://claude.com/resources/articles/the-ai-native-sdlc-playbook)
- [The AI-native SDLC playbook (blog version)](https://claude.com/blog/the-ai-native-sdlc-playbook)
- [Claude Academy course](https://academy.claude.com/courses/ai-native-sdlc-playbook)