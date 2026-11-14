import path from 'node:path';
import { breakpointAllowlist } from './breakpointAllowlist.js';

// shiplean/no-breakpoint-variants (K9): the touch layout has one breakpoint, the `touch:` (900px and below) and
// `phone:` (under 600px) variants. Width variants that act at or below 900px are banned in string literals and template
// text: every `max-[…]:`, the named `max-sm:` … `max-2xl:`, `min-[N]:` with N ≤ 900px (rem and em count 16px), `sm:`
// and `md:`. Desktop-only steps (`lg:`, `xl:`, `2xl:`, `min-[N]:` above 900px) never act at touch widths, so they stay.
// Existing uses are counted per file in breakpointAllowlist.js; a file must have exactly its count.

const TOUCH_MAX_PX = 900;
const VARIANT = /(?<![\w@-])(max-\[[^\]\s]*\]|max-(?:sm|md|lg|xl|2xl)|min-\[([^\]\s]*)\]|sm|md):/g;

function minWidthPx(value) {
  const match = /^(\d+(?:\.\d+)?)(px|rem|em)$/.exec(value);
  if (!match) return undefined;
  return match[2] === 'px' ? Number(match[1]) : Number(match[1]) * 16;
}

/** The banned variants in one piece of source text, e.g. ['md:', 'max-[620px]:']. */
export function bannedVariants(text) {
  const found = [];
  for (const match of text.matchAll(VARIANT)) {
    if (match[2] !== undefined) {
      const px = minWidthPx(match[2]);
      if (px !== undefined && px > TOUCH_MAX_PX) continue;
    }
    found.push(`${match[1]}:`);
  }
  return found;
}

function relativeToSrc(context) {
  return path.relative(path.join(context.cwd, 'src'), context.filename).split(path.sep).join('/');
}

export const noBreakpointVariants = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Bans width variants that act at or below 900px; use touch: or phone: (docs/mobile.md).',
    },
    schema: [
      {
        type: 'object',
        properties: { allowlist: { type: 'array' } },
        additionalProperties: false,
      },
    ],
    messages: {
      banned:
        "'{{variant}}' acts at or below 900px. Use touch: (900px and below) or phone: (under 600px); see docs/mobile.md.",
      more: '{{file}} has {{actual}} banned breakpoint variants but the allowlist allows {{allowed}}. Use touch: or phone: for the new ones.',
      fewer:
        '{{file}} has {{actual}} banned breakpoint variants but the allowlist says {{allowed}}: lower or delete the entry in eslint/breakpointAllowlist.js.',
    },
  },
  create(context) {
    const allowlist = context.options[0]?.allowlist ?? breakpointAllowlist;
    const file = relativeToSrc(context);
    const entry = allowlist.find((e) => e.file === file);
    let actual = 0;

    function check(node, text) {
      for (const variant of bannedVariants(text)) {
        actual += 1;
        if (!entry) context.report({ node, messageId: 'banned', data: { variant } });
      }
    }

    return {
      Literal(node) {
        if (typeof node.value === 'string') check(node, node.value);
      },
      TemplateElement(node) {
        check(node, node.value.raw);
      },
      'Program:exit'(node) {
        if (!entry || actual === entry.count) return;
        const data = { file, actual: String(actual), allowed: String(entry.count) };
        context.report({ node, messageId: actual > entry.count ? 'more' : 'fewer', data });
      },
    };
  },
};

export const breakpointsPlugin = {
  meta: { name: 'shiplean' },
  rules: { 'no-breakpoint-variants': noBreakpointVariants },
};
