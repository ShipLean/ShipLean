import { expect, test } from 'vitest';
import fixture from '../../../ShipLean.TestSupport/DtoFixtures/attachments/attachments.json';
import checkFixture from '../../../ShipLean.TestSupport/DtoFixtures/attachments/attachment-check.json';
import markdownFixture from '../../../ShipLean.TestSupport/DtoFixtures/attachments/attachment-markdown.json';
import type {
  AttachmentCheckDto,
  AttachmentCheckRequest,
  AttachmentDto,
  AttachmentMarkdownDto,
} from './attachments';

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

test('attachments_ShouldTypeEveryFixture', () => {
  const items = typed<AttachmentDto[]>()(fixture.attachments);
  expect(items.length).toBeGreaterThan(0);
  expect(items.some((item) => item.uploadedBy === null)).toBe(true);
  expect(items.some((item) => item.agentName !== null)).toBe(true);
  expect(new Set(items.map((item) => item.kind))).toEqual(new Set(['image', 'file', 'markdown']));
});

test('attachmentCheck_ShouldTypeEveryFixture', () => {
  typed<AttachmentCheckRequest>()(checkFixture.request);
  const response = typed<AttachmentCheckDto>()(checkFixture.response);
  expect(new Set(response.files.map((file) => file.outcome))).toEqual(
    new Set(['add', 'replace', 'tooLarge', 'overTotal']),
  );
});

test('attachmentMarkdown_ShouldTypeEveryFixture', () => {
  const view = typed<AttachmentMarkdownDto>()(markdownFixture.markdownView);
  expect(view.attachment.kind).toBe('markdown');
  expect(view.siblings.length).toBeGreaterThan(0);
});
