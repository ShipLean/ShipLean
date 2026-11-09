import { expect, test } from 'vitest';
import type { CriteriaDto } from './criteria';

// Literal unions widened to their base type, recursively: what a JSON import looks like.
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

// Maps every key the target type lacks to never, recursively, so a fixture property the type has dropped fails the check.
type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('criteria_ShouldTypeADtoWithClauseErrors', () => {
  const dto = typed<CriteriaDto>()({
    criteria: { match: 'all', clauses: [{ field: 'colour', op: '=', value: 'red' }] },
    errors: [
      {
        clauseIndex: 0,
        field: 'colour',
        code: 'unknownField',
        message: 'colour is not a field you can filter on.',
      },
    ],
  });
  const none = typed<CriteriaDto>()({ criteria: null, errors: [] });
  expect(dto.errors[0]?.clauseIndex).toBe(0);
  expect(none.criteria).toBeNull();
});
