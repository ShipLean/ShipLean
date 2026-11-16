import path from 'node:path';

// shiplean/no-client-dates (SHIP-1702 D7): every date and time on screen is a server label (DisplayDate, in the
// viewer's zone), so the client never builds or reads a date. Reports `new Date(...)`, Intl.DateTimeFormat and
// Intl.RelativeTimeFormat, toLocaleDateString/toLocaleTimeString, the Date getters (getHours … getFullYear,
// getTimezoneOffset, getUTC*) and the date libraries. Date.now(), Date.parse() and Date.UTC() stay allowed (epoch
// numbers for timers and comparisons), and so does a number's toLocaleString.
//
// Options: `allowlist`, files (from the package root) the rule skips while their lane fixes them
// (eslint/dates-allowlist.json); `exceptions`, permanent `{ file, allow }` entries that allow only the named constructs
// in that one file (e.g. `{ file: 'src/actions/clock.ts', allow: ['new Date'] }`).

const GETTERS = new Set([
  'getHours',
  'getMinutes',
  'getSeconds',
  'getMilliseconds',
  'getDate',
  'getDay',
  'getMonth',
  'getFullYear',
  'getYear',
  'getTimezoneOffset',
  'getUTCHours',
  'getUTCMinutes',
  'getUTCSeconds',
  'getUTCMilliseconds',
  'getUTCDate',
  'getUTCDay',
  'getUTCMonth',
  'getUTCFullYear',
]);
const LOCALE_FORMATS = new Set(['toLocaleDateString', 'toLocaleTimeString']);
const INTL_FORMATS = new Set(['DateTimeFormat', 'RelativeTimeFormat']);
const LIBRARIES = /^(dayjs|date-fns|moment|luxon)(\/|$)/;

function relativeToPackage(context) {
  return path.relative(context.cwd, context.filename).split(path.sep).join('/');
}

function propertyName(member) {
  return !member.computed && member.property.type === 'Identifier'
    ? member.property.name
    : undefined;
}

export const noClientDates = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Bans building or reading dates in the client; render the server label (.claude/skills/utc-everywhere).',
    },
    schema: [
      {
        type: 'object',
        properties: {
          allowlist: { type: 'array' },
          exceptions: { type: 'array' },
        },
        additionalProperties: false,
      },
    ],
    messages: {
      banned:
        "'{{construct}}' handles a date in the client. Render the server's label (DisplayDate, in the viewer's zone) or a server-computed field instead; see .claude/skills/utc-everywhere.",
    },
  },
  create(context) {
    const options = context.options[0] ?? {};
    const file = relativeToPackage(context);
    if ((options.allowlist ?? []).some((entry) => entry.file === file)) return {};
    const allowed = new Set(
      (options.exceptions ?? [])
        .filter((entry) => entry.file === file)
        .flatMap((entry) => entry.allow),
    );

    function report(node, construct) {
      if (!allowed.has(construct))
        context.report({ node, messageId: 'banned', data: { construct } });
    }

    function checkSource(node, source) {
      if (typeof source === 'string' && LIBRARIES.test(source)) report(node, source.split('/')[0]);
    }

    return {
      NewExpression(node) {
        if (node.callee.type === 'Identifier' && node.callee.name === 'Date')
          report(node, 'new Date');
      },
      MemberExpression(node) {
        const name = propertyName(node);
        if (
          node.object.type === 'Identifier' &&
          node.object.name === 'Intl' &&
          INTL_FORMATS.has(name)
        ) {
          report(node, `Intl.${name}`);
        }
      },
      CallExpression(node) {
        if (node.callee.type === 'MemberExpression') {
          const name = propertyName(node.callee);
          if (GETTERS.has(name) || LOCALE_FORMATS.has(name)) report(node, name);
        }
        if (node.callee.type === 'Identifier' && node.callee.name === 'require') {
          checkSource(node, node.arguments[0]?.value);
        }
      },
      ImportDeclaration(node) {
        checkSource(node, node.source.value);
      },
      ImportExpression(node) {
        checkSource(node, node.source.value);
      },
    };
  },
};
