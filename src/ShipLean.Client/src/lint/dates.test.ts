import path from 'node:path';
import { RuleTester } from 'eslint';
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
    allowlist: [{ file: 'src/features/harness/Listed.ts', owner: 'TEST', why: 'test' }],
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

describe('no-client-dates', () => {
  it('noClientDates_WhenCodeBuildsOrReadsADate_ShouldReport', () => {
    ruleTester.run('no-client-dates', noClientDates, {
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

  it('noClientDates_WhenEpochNumbersOrNumberFormats_ShouldAllow', () => {
    ruleTester.run('no-client-dates', noClientDates, {
      valid: [
        allowed('const t = Date.now();'),
        allowed('const t = Date.parse(iso);'),
        allowed('const t = Date.UTC(2026, 0, 1);'),
        allowed('const s = count.toLocaleString();'),
        allowed('const c = new Intl.Collator(undefined, { numeric: true });'),
        allowed(
          'const d = new Date(); const h = d.getHours();',
          filename('src/features/harness/Listed.ts'),
        ),
        allowed(
          'const z = Intl.DateTimeFormat().resolvedOptions().timeZone;',
          filename('src/features/harness/zone.ts'),
        ),
      ],
      invalid: [],
    });
  });
});
