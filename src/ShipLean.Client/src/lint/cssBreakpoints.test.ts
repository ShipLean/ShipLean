import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, it } from 'vitest';

// K1: plain CSS uses the same two media queries as the touch: and phone: variants, and no other width query.
const ALLOWED = ['(max-width: 900px)', '(max-width: 599.98px)'];
const srcDir = path.join(process.cwd(), 'src');

function cssFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return cssFiles(full);
    return entry.name.endsWith('.css') ? [full] : [];
  });
}

// Every width condition in an @media prelude or a @custom-variant's media query, e.g. "(max-width: 900px)".
function widthQueries(css: string): string[] {
  const preludes = [...css.matchAll(/@media([^{;]*)/g)].map((m) => m[1]);
  return preludes.flatMap((prelude) =>
    [...prelude.matchAll(/\(\s*(?:min-|max-)?width\s*[:<>=][^)]*\)/g)].map((m) =>
      m[0].replace(/\s+/g, ' '),
    ),
  );
}

it('cssMediaQueries_ShouldUseOnlyTheTouchAndPhoneWidths', () => {
  const files = cssFiles(srcDir);
  expect(files.length).toBeGreaterThan(0);
  const offending = files.flatMap((file) =>
    widthQueries(readFileSync(file, 'utf8'))
      .filter((query) => !ALLOWED.includes(query))
      .map((query) => `${path.relative(srcDir, file)}: ${query}`),
  );
  expect(offending).toEqual([]);
});
