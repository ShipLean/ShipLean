// Regenerates prompts-data.js for prompts.html (SHIP-744): a snapshot of the built-in prompt texts.
// Usage: node design/screens/prompts-data.mjs   (from the repository root)
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { execSync } from 'node:child_process';
import { join } from 'node:path';

const out = {};
for (const dir of ['src/ShipLean.WebApi/Features/Mcp/Planning/Guide', 'src/ShipLean.Application/Features/Demos/Prompt'])
  for (const f of readdirSync(dir).filter(f => f.endsWith('.md'))) out[f] = readFileSync(join(dir, f), 'utf8');
const cs = readFileSync('src/ShipLean.Application/Features/IssueIntake/Rounds/DefaultProposalInstructions.cs', 'utf8');
out['issue-proposal.md'] = cs.match(/Text = """\n([\s\S]*?)\n\s*""";/)[1].split('\n').map(l => l.replace(/^ {8}/, '')).join('\n');
const sha = execSync('git rev-parse --short HEAD').toString().trim();
writeFileSync('design/screens/prompts-data.js',
  `// Built-in prompt texts at ${sha}, for prompts.html. Made by prompts-data.mjs; don't edit by hand.\n` +
  `window.BUILTIN = ${JSON.stringify(out, null, 1)};\nwindow.BUILTIN_SHA = "${sha}";\n`);
console.log(`${Object.keys(out).length} prompts at ${sha}`);
