// Stop hook: when backend or frontend files changed, their verification command must pass before Claude reports done.
// - Backend (.cs, .csproj, .slnx, .props, .targets, global.json): `dotnet test` (CLAUDE.md, Commands).
// - Frontend (src/CalendarPlaybook.Web/): `npm test` then `npm run build` (CLAUDE.md, Commands).
// Skips a group when none of its files changed, or when the same change set already passed.
const fs = require("fs");
const os = require("os");
const path = require("path");
const crypto = require("crypto");
const { execSync, spawnSync } = require("child_process");

const input = JSON.parse(fs.readFileSync(0, "utf8") || "{}");
if (input.stop_hook_active) process.exit(0); // already blocked once this turn; avoid loops

const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const web = path.join(root, "src", "CalendarPlaybook.Web");

const groups = [
  {
    name: "backend",
    matches: file => /\.(cs|csproj|slnx|props|targets)$|(^|\/)global\.json$/.test(file),
    run: () => spawnSync("dotnet", ["test", "--nologo"], { cwd: root, encoding: "utf8", shell: true }),
    describe: "'dotnet test'",
  },
  {
    name: "web",
    matches: file => file.startsWith("src/CalendarPlaybook.Web/"),
    run: () => {
      const test = spawnSync("npm", ["test"], { cwd: web, encoding: "utf8", shell: true });
      if (test.status !== 0) return test;
      return spawnSync("npm", ["run", "build"], { cwd: web, encoding: "utf8", shell: true });
    },
    describe: "'npm test' and 'npm run build' in src/CalendarPlaybook.Web",
  },
];

let status;
try {
  status = execSync("git status --porcelain -uall", { cwd: root, encoding: "utf8" });
} catch {
  process.exit(0);
}

const allChanged = status.split("\n")
  .filter(Boolean)
  .map(line => line.slice(3).replace(/^.* -> /, "").replace(/^"|"$/g, ""))
  .sort();

for (const group of groups) {
  const changed = allChanged.filter(group.matches);
  if (changed.length === 0) continue;

  const hash = crypto.createHash("sha256");
  for (const file of changed) {
    hash.update(file);
    const full = path.join(root, file);
    if (fs.existsSync(full)) hash.update(fs.readFileSync(full));
  }
  const digest = hash.digest("hex");
  const cacheFile = path.join(os.tmpdir(), `calendarplaybook-last-green-${group.name}-` +
    crypto.createHash("sha1").update(root).digest("hex").slice(0, 12));
  if (fs.existsSync(cacheFile) && fs.readFileSync(cacheFile, "utf8") === digest) continue;

  const result = group.run();
  if (result.status === 0) {
    fs.writeFileSync(cacheFile, digest);
    continue;
  }

  const output = `${result.stdout || ""}${result.stderr || ""}`.split("\n").slice(-40).join("\n");
  process.stdout.write(JSON.stringify({
    decision: "block",
    reason: `Verification failed: ${group.describe} did not pass for the changed ${group.name} files. Fix it before reporting done (CLAUDE.md, Workflow). Last output:\n${output}`,
  }));
  process.exit(0);
}

process.exit(0);
