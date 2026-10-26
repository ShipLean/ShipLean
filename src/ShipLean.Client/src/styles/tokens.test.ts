import { readFileSync } from 'node:fs';
import { expect, test } from 'vitest';

// The rules only: comments, prettier's quote and zero style, whitespace and the font import (the one intended difference) don't count.
const body = (css: string) =>
  css
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/@import url\("[^"]*"\);/g, '')
    .replace(/'/g, '"')
    .replace(/(?<![\d.])0\.(\d)/g, '.$1')
    .replace(/\s+/g, ' ')
    .trim();

test('tokens_ShouldMatchTheDesignFile', () => {
  const copy = readFileSync('src/styles/tokens.css', 'utf8');
  const design = readFileSync('../../design/tokens.css', 'utf8');

  expect(body(copy)).toBe(body(design));
});
