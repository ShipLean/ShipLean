import path from 'node:path';
import { RuleTester } from 'eslint';
import tseslint from 'typescript-eslint';
import { describe, it } from 'vitest';
import { noBreakpointVariants } from '../../eslint/breakpoints.js';

// RuleTester runs its cases inside these hooks; each `it` below owns its cases, so failures throw inside it.
RuleTester.describe = (_name, fn) => fn();
RuleTester.it = (_name, fn) => fn();
RuleTester.itOnly = RuleTester.it;

const ruleTester = new RuleTester({
  languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true } } },
});

const filename = (file: string) => path.join(process.cwd(), 'src', file);
const harness = filename('features/harness/Harness.tsx');
const allowlist = [{ file: 'features/harness/Listed.tsx', count: 2, owner: 'TEST' }];
const listed = filename('features/harness/Listed.tsx');
const options = [{ allowlist }];

const banned = (code: string, ...variants: string[]) => ({
  code,
  filename: harness,
  options,
  errors: variants.map((variant) => ({ messageId: 'banned', data: { variant } })),
});

describe('no-breakpoint-variants', () => {
  it('breakpointRule_WhenArbitraryWidthVariant_ShouldReport', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [],
      invalid: [
        banned(`const c = 'flex max-[900px]:hidden';`, 'max-[900px]:'),
        banned(`const c = 'max-[620px]:grid-cols-1';`, 'max-[620px]:'),
        banned(`const c = <div className="min-[761px]:flex" />;`, 'min-[761px]:'),
        banned(`const c = 'min-[56rem]:flex';`, 'min-[56rem]:'),
        banned('const c = `p-2 ${x} max-[899px]:p-0`;', 'max-[899px]:'),
      ],
    });
  });

  it('breakpointRule_WhenSmPrefix_ShouldReport', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [],
      invalid: [
        banned(`const c = 'grid sm:grid-cols-2';`, 'sm:'),
        banned(`const c = 'md:flex';`, 'md:'),
        banned(`const c = 'dark:md:flex hover:sm:underline';`, 'md:', 'sm:'),
      ],
    });
  });

  it('breakpointRule_WhenNamedMaxVariant_ShouldReport', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [],
      invalid: [
        banned(`const c = 'max-sm:flex-col';`, 'max-sm:'),
        banned(`const c = 'max-lg:hidden';`, 'max-lg:'),
        banned(`const c = 'max-md:p-0 max-xl:p-1 max-2xl:p-2';`, 'max-md:', 'max-xl:', 'max-2xl:'),
      ],
    });
  });

  it('breakpointRule_WhenTouchOrPhone_ShouldPass', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [
        { code: `const c = 'touch:hidden phone:flex-col';`, filename: harness, options },
        { code: `const c = 'lg:flex xl:grid 2xl:p-4';`, filename: harness, options },
        {
          code: `const c = 'min-[1251px]:flex min-[901px]:grid min-[60rem]:p-1';`,
          filename: harness,
          options,
        },
        { code: `const c = '@md:flex'; const size = { md: 20 };`, filename: harness, options },
      ],
      invalid: [],
    });
  });

  it('breakpointRule_WhenAllowlistedFileHasItsCount_ShouldPass', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [{ code: `const a = 'sm:flex'; const b = 'max-sm:p-0';`, filename: listed, options }],
      invalid: [],
    });
  });

  it('breakpointRule_WhenAllowlistedFileHasMore_ShouldReport', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [],
      invalid: [
        {
          code: `const a = 'sm:flex'; const b = 'max-sm:p-0 md:grid';`,
          filename: listed,
          options,
          errors: [
            {
              messageId: 'more',
              data: { file: 'features/harness/Listed.tsx', actual: '3', allowed: '2' },
            },
          ],
        },
      ],
    });
  });

  it('breakpointRule_WhenAllowlistedFileHasFewer_ShouldReport', () => {
    ruleTester.run('no-breakpoint-variants', noBreakpointVariants, {
      valid: [],
      invalid: [
        {
          code: `const a = 'touch:flex'; const b = 'max-sm:p-0';`,
          filename: listed,
          options,
          errors: [
            {
              messageId: 'fewer',
              data: { file: 'features/harness/Listed.tsx', actual: '1', allowed: '2' },
            },
          ],
        },
      ],
    });
  });
});
