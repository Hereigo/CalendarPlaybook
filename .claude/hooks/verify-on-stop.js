// Stop hook: when backend files changed, `dotnet test` must pass before Claude reports done.
// Skips when nothing relevant changed, or when the same change set already passed.
const fs = require("fs");
const os = require("os");
const path = require("path");
const crypto = require("crypto");
const { execSync, spawnSync } = require("child_process");

const input = JSON.parse(fs.readFileSync(0, "utf8") || "{}");
if (input.stop_hook_active) process.exit(0); // already blocked once this turn; avoid loops

const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const relevant = /\.(cs|csproj|slnx|props|targets)$|(^|\/)global\.json$/;

let status;
try {
  status = execSync("git status --porcelain -uall", { cwd: root, encoding: "utf8" });
} catch {
  process.exit(0);
}

const changed = status.split("\n")
  .filter(Boolean)
  .map(line => line.slice(3).replace(/^.* -> /, "").replace(/^"|"$/g, ""))
  .filter(file => relevant.test(file))
  .sort();
if (changed.length === 0) process.exit(0);

const hash = crypto.createHash("sha256");
for (const file of changed) {
  hash.update(file);
  const full = path.join(root, file);
  if (fs.existsSync(full)) hash.update(fs.readFileSync(full));
}
const digest = hash.digest("hex");
const cacheFile = path.join(os.tmpdir(), "calendarplaybook-last-green-" +
  crypto.createHash("sha1").update(root).digest("hex").slice(0, 12));
if (fs.existsSync(cacheFile) && fs.readFileSync(cacheFile, "utf8") === digest) process.exit(0);

const result = spawnSync("dotnet", ["test", "--nologo"], { cwd: root, encoding: "utf8", shell: true });
if (result.status === 0) {
  fs.writeFileSync(cacheFile, digest);
  process.exit(0);
}

const output = `${result.stdout || ""}${result.stderr || ""}`.split("\n").slice(-40).join("\n");
process.stdout.write(JSON.stringify({
  decision: "block",
  reason: `Verification failed: 'dotnet test' did not pass for the changed backend files. Fix it before reporting done (CLAUDE.md, Workflow). Last output:\n${output}`,
}));
