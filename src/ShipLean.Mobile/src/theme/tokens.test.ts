import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const script = resolve(__dirname, '../../scripts/generate-theme.mjs');
const tokensPath = resolve(__dirname, '../../../../design/tokens.json');

function generate(tokens: string): string {
  return execFileSync('node', [script, '--stdout', tokens], { encoding: 'utf8' });
}

describe('theme tokens', () => {
  it('generated theme matches design/tokens.json', () => {
    const committed = readFileSync(join(__dirname, 'tokens.generated.ts'), 'utf8');
    expect(
      generate(tokensPath),
      'src/theme/tokens.generated.ts is stale; run `pnpm run theme` in src/ShipLean.Mobile and commit it',
    ).toBe(committed);
  });

  it('applies the touch font override to the input size only', () => {
    const tokens = JSON.parse(readFileSync(tokensPath, 'utf8'));
    tokens.touch['text-input-touch'] = '18px';
    const dir = mkdtempSync(join(tmpdir(), 'tokens-'));
    const file = join(dir, 'tokens.json');
    writeFileSync(file, JSON.stringify(tokens));
    const out = generate(file);
    expect(out).toContain('input: 18,');
    expect(out).toContain('base: 13,');
  });
});

describe('type colours', () => {
  it('emits colors.type for every work item type in both schemes', () => {
    const tokens = JSON.parse(readFileSync(tokensPath, 'utf8'));
    const out = generate(tokensPath);
    for (const scheme of ['light', 'dark'] as const) {
      for (const [id, hex] of Object.entries<string>(tokens.type[scheme])) {
        expect(out).toContain(`${id}: '${hex}',`);
      }
    }
    expect(out).toContain('type: {');
  });
});
