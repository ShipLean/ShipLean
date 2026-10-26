import { expect, test } from 'vitest';
import fixture from '../../../../ShipLean.TestSupport/DtoFixtures/auth/instance.json';
import type { InstanceDto } from './authApi';

// Typecheck fails if a fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
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

const SIGN_IN_MODES: InstanceDto['signIn'][] = ['password', 'proxy'];

test('instance_ShouldTypeBothFixtures', () => {
  const password = typed<InstanceDto>()(fixture.password);
  const proxy = typed<InstanceDto>()(fixture.proxy);

  expect(SIGN_IN_MODES).toContain(password.signIn);
  expect(password.signOutUrl).toBeNull();
  expect(SIGN_IN_MODES).toContain(proxy.signIn);
  expect(proxy.signOutUrl).toBe('/oauth2/sign_out');
});
