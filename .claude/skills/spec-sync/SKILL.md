---
name: spec-sync
description: Keep code, plan.md and the intent/spec in step during the AI-native SDLC. Use when implementation needs to differ from intent/calendar-app-spec.md, when a §13 decision or open question is affected, when starting a new piece of work, or when a session ends with work unfinished.
---

# Keeping the SDLC artifacts in sync

Artifacts: `intent/calendar-app.md` (intent), `intent/calendar-app-spec.md` (spec), `plan.md` (current plan), `Continue_here.md` (handoff).

## Before building

- Start in plan mode. Name the spec sections the work implements.
- When the plan is approved, write or update `plan.md` at the repo root: goal, spec sections, steps, verification command, open points.

## When the code must differ from the spec

1. Stop. Do not implement the divergence silently.
2. Tell the user what differs, which spec section, and why.
3. If it touches a §13 decision, it needs product-owner review. Propose the wording; don't change §13 yourself.
4. After approval, update the spec (edits to `intent/` ask for confirmation via a hook), then `plan.md`.

## When a session ends unfinished

Write `Continue_here.md`: what is done, what is next, failing tests, decisions pending. Delete it when the steps are done.

## Closing the loop

Recurring mistakes go into `CLAUDE.md` (one line each) or into a skill. Rules that must never be broken go into a hook in `.claude/settings.json`.
