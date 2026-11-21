import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, it } from 'vitest';

// SHIP-1516: the theme maps the accent tokens to Tailwind as signal, signal-soft and signal-ink.
// A `bg-accent`-style utility compiles to no CSS at all, so it must not appear in the sources.
const srcDir = path.join(process.cwd(), 'src');
const DEAD =
  /(?<![\w-])(?:[\w[\]=:-]+:)?(?:bg|text|border|ring|outline|fill|stroke|from|to|via|divide|placeholder|caret|decoration|shadow)-accent(?:-soft|-fg)?(?![\w-])/g;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    return /\.(tsx?|jsx?)$/.test(entry.name) && !entry.name.includes('accentClasses') ? [full] : [];
  });
}

it('tailwindClasses_ShouldNotUseTheUnmappedAccentColour', () => {
  const files = sourceFiles(srcDir);
  expect(files.length).toBeGreaterThan(0);
  const offending = files.flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(DEAD)].map(
      (m) =>
        `${path.relative(srcDir, file)}: ${m[0]} (use the signal, signal-soft or signal-ink colour)`,
    ),
  );
  expect(offending).toEqual([]);
});
