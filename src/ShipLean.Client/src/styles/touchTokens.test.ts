import { readFileSync } from 'node:fs';
import { expect, test } from 'vitest';

// K1's settled touch tokens (SHIP-805 rules 9, 10 and 13). tokens_ShouldMatchTheDesignFile keeps the copies equal.
const tokens = readFileSync('src/styles/tokens.css', 'utf8');

function token(name: string): string | undefined {
  return new RegExp(`^\\s*--${name}:\\s*([^;]+);`, 'm').exec(tokens)?.[1].trim();
}

test('touchTokens_ShouldHaveTheSettledValues', () => {
  expect({
    touchTarget: token('touch-target'),
    mobileBarHeight: token('mobile-bar-height'),
    drawerWidth: token('drawer-width'),
    textInputTouch: token('text-input-touch'),
    textBodyTouch: token('text-body-touch'),
    scrimTouch: token('scrim-touch'),
  }).toEqual({
    touchTarget: '32px',
    mobileBarHeight: '48px',
    drawerWidth: '304px',
    textInputTouch: '16px',
    textBodyTouch: '15px',
    scrimTouch: 'rgb(10 16 28 / 0.45)',
  });
  // Set once in the light :root and never redefined, so dark gets the same values.
  expect(tokens.match(/--scrim-touch:/g)).toHaveLength(1);
  expect(tokens.match(/--touch-target:/g)).toHaveLength(1);
});

test('indexHtml_ShouldAllowViewportFitCover', () => {
  const html = readFileSync('index.html', 'utf8');
  const viewport = /<meta\s+name="viewport"\s+content="([^"]*)"/.exec(html)?.[1];

  expect(viewport?.split(',').map((part) => part.trim())).toContain('viewport-fit=cover');
});
