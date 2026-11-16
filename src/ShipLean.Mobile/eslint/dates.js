// CommonJS like the rest of this package's config. A copy of src/ShipLean.Client/eslint/dates.js.
const path = require('node:path');

// shiplean/no-client-dates (SHIP-1702 D7): every date and time on screen is a server label (DisplayDate, in the
// viewer's zone), so the client never builds or reads a date. Reports `new Date(...)`, Intl.DateTimeFormat and
// Intl.RelativeTimeFormat, toLocaleDateString/toLocaleTimeString, the Date getters (getHours … getFullYear,
// getTimezoneOffset, getUTC*) and the date libraries. Date.now(), Date.parse() and Date.UTC() stay allowed (epoch
// numbers for timers and comparisons), and so does a number's toLocaleString.
//
// Static computed access (`Intl['DateTimeFormat']`, `at['getHours']()`) and the globals' Date and Intl
// (`new globalThis.Date()`, `window.Intl.DateTimeFormat`) count too. A non-Date `getHours`, or a non-number
// `toLocaleString`, is not told apart (no type information).
//
// Options: `exceptions`, permanent `{ file, allow }` entries that allow only the named constructs
// in that one file (e.g. `{ file: 'src/actions/clock.ts', allow: ['new Date'] }`). An allowed 'new Date' covers only
// the first construction in the file. An allowed 'Intl.DateTimeFormat' covers only one read of the browser's zone,
// `Intl.DateTimeFormat().resolvedOptions().timeZone`, once in the file.

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
const GLOBALS = new Set(['globalThis', 'window', 'self', 'global']);

function relativeToPackage(context) {
  return path.relative(context.cwd, context.filename).split(path.sep).join('/');
}

// The property's name for `a.name`, `a['name']` and a template literal without holes; undefined when computed.
function propertyName(member) {
  const property = member.property;
  if (!member.computed) return property.type === 'Identifier' ? property.name : undefined;
  if (property.type === 'Literal' && typeof property.value === 'string') return property.value;
  if (property.type === 'TemplateLiteral' && property.expressions.length === 0)
    return property.quasis[0].value.cooked;
  return undefined;
}

// `name`, or the global object's `name` (`globalThis.Date`, `window['Intl']`).
function isGlobal(node, name) {
  if (node.type === 'Identifier') return node.name === name;
  return (
    node.type === 'MemberExpression' &&
    node.object.type === 'Identifier' &&
    GLOBALS.has(node.object.name) &&
    propertyName(node) === name
  );
}

// True for the `Intl.DateTimeFormat` of `Intl.DateTimeFormat().resolvedOptions().timeZone`.
function isZoneRead(node) {
  const call = node.parent;
  if (call?.type !== 'CallExpression' || call.callee !== node || call.arguments.length > 0)
    return false;
  const options = call.parent;
  if (
    options?.type !== 'MemberExpression' ||
    options.object !== call ||
    propertyName(options) !== 'resolvedOptions'
  )
    return false;
  const read = options.parent;
  if (read?.type !== 'CallExpression' || read.callee !== options || read.arguments.length > 0)
    return false;
  const zone = read.parent;
  return (
    zone?.type === 'MemberExpression' && zone.object === read && propertyName(zone) === 'timeZone'
  );
}

const noClientDates = {
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
    const allowed = new Set(
      (options.exceptions ?? [])
        .filter((entry) => entry.file === file)
        .flatMap((entry) => entry.allow),
    );

    let zoneReads = 0;
    let dateConstructions = 0;
    function isAllowed(node, construct) {
      if (!allowed.has(construct)) return false;
      if (construct === 'new Date') {
        dateConstructions += 1;
        return dateConstructions === 1;
      }
      if (construct !== 'Intl.DateTimeFormat') return true;
      if (!isZoneRead(node)) return false;
      zoneReads += 1;
      return zoneReads === 1;
    }

    function report(node, construct) {
      if (!isAllowed(node, construct))
        context.report({ node, messageId: 'banned', data: { construct } });
    }

    function checkSource(node, source) {
      if (typeof source === 'string' && LIBRARIES.test(source)) report(node, source.split('/')[0]);
    }

    return {
      NewExpression(node) {
        if (isGlobal(node.callee, 'Date')) report(node, 'new Date');
      },
      MemberExpression(node) {
        const name = propertyName(node);
        if (isGlobal(node.object, 'Intl') && INTL_FORMATS.has(name)) report(node, `Intl.${name}`);
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

module.exports = { noClientDates };
