import { expect, test } from 'vitest';
import fixture from '../../../ShipLean.TestSupport/DtoFixtures/markdown/rendered-images.json';
import type { AttachmentDto } from './attachments';
import type { RenderedMarkdownDto } from './markdown';

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

test('renderedImages_ShouldTypeEveryFixture', () => {
  typed<AttachmentDto[]>()(fixture.attachments);
  const rendered = typed<RenderedMarkdownDto>()(fixture.rendered);
  expect(new Set(rendered.images.map((image) => image.status))).toEqual(
    new Set(['attached', 'external', 'missing']),
  );
});
