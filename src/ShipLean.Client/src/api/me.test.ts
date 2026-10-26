import { expect, test } from 'vitest';
import me from '../../../ShipLean.TestSupport/PinnedShapes/me.json';
import { DENSITIES, THEMES, type MeDto } from './me';

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('me_ShouldTypeTheFixture', () => {
  const joel = typed<MeDto>()(me.me);

  expect(joel.status).toBe('active');
  expect(THEMES).toContain(joel.theme);
  expect(DENSITIES).toContain(joel.density);
  // The preference rows read through the same vocabularies.
  const preference = (key: string) => me.preferences.find((row) => row.key === key)?.json;
  expect(THEMES).toContain(preference('theme'));
  expect(DENSITIES).toContain(preference('density'));
});
