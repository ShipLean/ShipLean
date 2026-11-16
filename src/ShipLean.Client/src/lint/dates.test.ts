import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { RuleTester, type Rule } from 'eslint';
import tseslint from 'typescript-eslint';
import { describe, it } from 'vitest';
import { noClientDates } from '../../eslint/dates.js';

// RuleTester runs its cases inside these hooks; each `it` below owns its cases, so failures throw inside it.
RuleTester.describe = (_name, fn) => fn();
RuleTester.it = (_name, fn) => fn();
RuleTester.itOnly = RuleTester.it;

const ruleTester = new RuleTester({
  languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true } } },
});

const filename = (file: string) => path.join(process.cwd(), file);
const harness = filename('src/features/harness/Harness.tsx');
const options = [
  {
    exceptions: [{ file: 'src/features/harness/zone.ts', allow: ['Intl.DateTimeFormat'] }],
  },
];

const banned = (code: string, construct: string, file = harness) => ({
  code,
  filename: file,
  options,
  errors: [{ messageId: 'banned', data: { construct } }],
});
const allowed = (code: string, file = harness) => ({ code, filename: file, options });
const zone = filename('src/features/harness/zone.ts');

// The phone app and @shiplean/core carry copies of this rule; every case below runs against all three.
async function copy(packageFolder: string): Promise<Rule.RuleModule> {
  const file = path.join(process.cwd(), '..', packageFolder, 'eslint', 'dates.js');
  const loaded: { noClientDates?: Rule.RuleModule; default?: { noClientDates: Rule.RuleModule } } =
    await import(pathToFileURL(file).href);
  const rule = loaded.noClientDates ?? loaded.default?.noClientDates;
  if (!rule) throw new Error(`${file} exports no noClientDates`);
  return rule;
}

const copies: [string, () => Promise<Rule.RuleModule>][] = [
  ['ShipLean.Client', () => Promise.resolve(noClientDates)],
  ['ShipLean.Mobile', () => copy('ShipLean.Mobile')],
  ['ShipLean.Core.Client', () => copy('ShipLean.Core.Client')],
];

describe.each(copies)('no-client-dates (%s)', (_name, load) => {
  it('noClientDates_WhenCodeBuildsOrReadsADate_ShouldReport', async () => {
    const rule = await load();
    ruleTester.run('no-client-dates', rule, {
      valid: [],
      invalid: [
        banned('const d = new Date();', 'new Date'),
        banned('const d = new Date(iso);', 'new Date'),
        banned('const f = new Intl.DateTimeFormat("en");', 'Intl.DateTimeFormat'),
        banned(
          'const z = Intl.DateTimeFormat().resolvedOptions().timeZone;',
          'Intl.DateTimeFormat',
        ),
        banned('const r = new Intl.RelativeTimeFormat("en");', 'Intl.RelativeTimeFormat'),
        banned('const s = at.toLocaleDateString();', 'toLocaleDateString'),
        banned('const s = at.toLocaleTimeString();', 'toLocaleTimeString'),
        banned('const h = at.getHours();', 'getHours'),
        banned('const o = at.getTimezoneOffset();', 'getTimezoneOffset'),
        banned('const d = at.getUTCDay();', 'getUTCDay'),
        banned("import dayjs from 'dayjs';", 'dayjs'),
        banned("import { format } from 'date-fns/format';", 'date-fns'),
        banned("const { DateTime } = require('luxon');", 'luxon'),
        banned("const m = await import('moment');", 'moment'),
        banned('const d = new Date();', 'new Date', filename('src/features/harness/zone.ts')),
      ],
    });
  });

  it('noClientDates_WhenStaticComputedAccessOrAGlobalObject_ShouldReport', async () => {
    const rule = await load();
    ruleTester.run('no-client-dates', rule, {
      valid: [],
      invalid: [
        banned("const f = Intl['DateTimeFormat']('en');", 'Intl.DateTimeFormat'),
        banned('const f = new Intl[`RelativeTimeFormat`]();', 'Intl.RelativeTimeFormat'),
        banned("const h = at['getHours']();", 'getHours'),
        banned('const s = at[`toLocaleTimeString`]();', 'toLocaleTimeString'),
        banned('const d = new globalThis.Date();', 'new Date'),
        banned('const d = new window.Date(iso);', 'new Date'),
        banned("const d = new self['Date']();", 'new Date'),
        banned("const f = new window.Intl.DateTimeFormat('en');", 'Intl.DateTimeFormat'),
        banned("const f = globalThis['Intl'].DateTimeFormat('en');", 'Intl.DateTimeFormat'),
      ],
    });
  });

  it('noClientDates_WhenDynamicAccessOrAnotherObject_ShouldAllow', async () => {
    const rule = await load();
    ruleTester.run('no-client-dates', rule, {
      valid: [
        allowed('const v = at[key]();'),
        allowed('const t = globalThis.Date.now();'),
        allowed('const d = new calendar.Date();'),
        allowed('const f = formats.Intl.DateTimeFormat;'),
      ],
      invalid: [],
    });
  });

  it('noClientDates_WhenTheZoneExceptionIsUsedForMoreThanOneZoneRead_ShouldReport', async () => {
    const rule = await load();
    ruleTester.run('no-client-dates', rule, {
      valid: [],
      invalid: [
        banned("const f = new Intl.DateTimeFormat('en');", 'Intl.DateTimeFormat', zone),
        banned("const f = Intl.DateTimeFormat('en').format(at);", 'Intl.DateTimeFormat', zone),
        banned(
          "const z = Intl.DateTimeFormat('en').resolvedOptions().timeZone;",
          'Intl.DateTimeFormat',
          zone,
        ),
        banned('const o = Intl.DateTimeFormat().resolvedOptions();', 'Intl.DateTimeFormat', zone),
        banned(
          'const l = Intl.DateTimeFormat().resolvedOptions().locale;',
          'Intl.DateTimeFormat',
          zone,
        ),
        banned(
          'const a = Intl.DateTimeFormat().resolvedOptions().timeZone;\nconst b = Intl.DateTimeFormat().resolvedOptions().timeZone;',
          'Intl.DateTimeFormat',
          zone,
        ),
        banned('const r = new Intl.RelativeTimeFormat();', 'Intl.RelativeTimeFormat', zone),
      ],
    });
  });

  it('noClientDates_WhenEpochNumbersOrNumberFormats_ShouldAllow', async () => {
    const rule = await load();
    ruleTester.run('no-client-dates', rule, {
      valid: [
        allowed('const t = Date.now();'),
        allowed('const t = Date.parse(iso);'),
        allowed('const t = Date.UTC(2026, 0, 1);'),
        allowed('const s = count.toLocaleString();'),
        allowed('const c = new Intl.Collator(undefined, { numeric: true });'),
        allowed(
          'const z = Intl.DateTimeFormat().resolvedOptions().timeZone;',
          filename('src/features/harness/zone.ts'),
        ),
      ],
      invalid: [],
    });
  });
});
