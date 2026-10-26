import { expect, test } from 'vitest';
import me from '../../../ShipLean.TestSupport/PinnedShapes/me.json';
import { USER_STATUSES, type UserDto } from './users';

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

test('users_ShouldTypeTheFixture', () => {
  const users = typed<UserDto[]>()(me.users);

  expect(users.map((user) => user.status)).toEqual([
    'active',
    'oneTimePassword',
    'locked',
    'noPassword',
    'disabled',
  ]);
  expect(new Set(users.map((user) => user.status))).toEqual(new Set(USER_STATUSES));
});
