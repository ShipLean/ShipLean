// Writes src/theme/tokens.generated.ts from design/tokens.json. Plain Node, no dependencies.
//   node scripts/generate-theme.mjs                     write the file
//   node scripts/generate-theme.mjs --stdout [tokens]   print it (the test compares this with the committed file)
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const defaultTokens = resolve(here, '../../../design/tokens.json');
const outFile = resolve(here, '../src/theme/tokens.generated.ts');

const camel = (key) => key.replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());
// "10.5px" -> 10.5; fixed formatting so the output never depends on how the JSON spells a number.
const px = (value) => {
  const n = Number.parseFloat(value);
  if (!Number.isFinite(n)) throw new Error(`Not a pixel value: ${value}`);
  return String(Number(n.toFixed(2)));
};
// Font size keys start with a digit in the tokens ("2xs"); the app's keys are identifiers (xxs).
const sizeKey = (key) => key.replace(/^2/, 'xx').replace(/^(x+)xs$/, '$1s');
const sorted = (entries) => [...entries].sort(([a], [b]) => a.localeCompare(b));

function object(entries, indent) {
  const pad = '  '.repeat(indent);
  const lines = sorted(entries).map(
    ([k, v]) => `${pad}  ${/^[A-Za-z_]\w*$/.test(k) ? k : `'${k}'`}: ${v},`,
  );
  return `{\n${lines.join('\n')}\n${pad}}`;
}
const str = (s) => `'${s}'`;

export function render(tokens) {
  const colorSet = (scheme) =>
    object(
      Object.entries(tokens.color[scheme]).map(([k, v]) => [camel(k), str(v)]),
      2,
    );
  const sizes = Object.entries(tokens.text).map(([k, v]) => [sizeKey(k), px(v)]);
  // Touch override: input text is the touch size, so iOS never zooms into a field.
  sizes.push(['input', px(tokens.touch['text-input-touch'])]);
  const category = Object.entries(tokens['state-category']).map(([k, v]) => [
    camel(k),
    str(camel(v)),
  ]);
  const weights = [
    ['regular', "'400'"],
    ['medium', "'500'"],
    ['semibold', "'600'"],
    ['bold', "'700'"],
  ];
  const numbers = (group) =>
    object(
      Object.entries(group).map(([k, v]) => [k, px(v)]),
      1,
    );

  return `// Generated from design/tokens.json; do not edit. Run \`pnpm run theme\` to regenerate.
export const generatedTokens = {
  colors: {
    dark: ${colorSet('dark')},
    light: ${colorSet('light')},
  },
  font: {
    size: ${object(sizes, 2)},
    weight: ${object(weights, 2)},
  },
  radius: ${numbers(tokens.radius)},
  space: ${numbers(tokens.space)},
  // Colour names in each scheme's colours.
  stateCategory: ${object(category, 1)},
  touch: ${object(
    [
      ['bar', px(tokens.touch['mobile-bar-height'])],
      ['control', px(tokens.touch['touch-target'])],
      ['row', px(tokens.touch['touch-target'])],
    ],
    1,
  )},
} as const;
`;
}

const args = process.argv.slice(2);
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const toStdout = args[0] === '--stdout';
  const tokensPath = (toStdout ? args[1] : args[0]) ?? defaultTokens;
  const text = render(JSON.parse(readFileSync(tokensPath, 'utf8')));
  if (toStdout) process.stdout.write(text);
  else writeFileSync(outFile, text);
}
