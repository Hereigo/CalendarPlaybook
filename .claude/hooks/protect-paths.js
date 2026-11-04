// PreToolUse hook (Edit|Write|NotebookEdit): guards files that agents must not change freely.
// - intent/: reviewed artifacts (intent, spec). Edits need the user's confirmation.
// - Infrastructure/Migrations/: existing migrations are immutable. Add a new migration instead.
// - Config files: block secrets (passwords, client secrets) from being written into the repo.
const fs = require("fs");
const path = require("path");

const input = JSON.parse(fs.readFileSync(0, "utf8") || "{}");
const toolInput = input.tool_input || {};
const filePath = toolInput.file_path || toolInput.notebook_path;
if (!filePath) process.exit(0);

const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const rel = path.relative(root, path.resolve(root, filePath)).split(path.sep).join("/");
if (rel.startsWith("..")) process.exit(0);

function decide(permissionDecision, reason) {
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision,
      permissionDecisionReason: reason,
    },
  }));
  process.exit(0);
}

if (rel.startsWith("intent/")) {
  decide("ask", `${rel} is a reviewed SDLC artifact (intent/spec). Changes need the originator's or product owner's approval; spec §13 decisions must not change silently.`);
}

const migration = /(^|\/)Infrastructure\/Migrations\/[^/]+$/.test(rel);
if (migration && !/ModelSnapshot\.cs$/.test(rel) && fs.existsSync(path.join(root, rel))) {
  decide("deny", `${rel} is an existing EF Core migration and may already be applied. Do not edit it; add a new migration with 'dotnet ef migrations add <Name>' instead.`);
}

const configFile = /(^|\/)(appsettings[^/]*\.json|launchSettings\.json|[^/]*\.ya?ml|\.env[^/]*)$/i.test(rel);
if (configFile) {
  const text = [toolInput.content, toolInput.new_string].filter(Boolean).join("\n");
  const secret = /(password|pwd|client_?secret|sa_password|mssql_sa_password)\s*["']?\s*[:=]\s*["']?(?!\$\{|\$\(|<|["']?\s*$)[^\s"';,]{3,}/i;
  if (secret.test(text)) {
    decide("deny", `${rel} would contain a secret. Secrets (connection-string passwords, cidaas secrets, sa password) come from environment variables or user secrets, never committed files.`);
  }
}

process.exit(0);
