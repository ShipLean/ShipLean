import { expect, test } from 'vitest';
import fixture from '../../../ShipLean.TestSupport/DtoFixtures/links/link-cards.json';
import {
  LINK_GROUPS,
  LINK_KINDS,
  LINK_PREVIEW_STATUSES,
  LINK_STATE_SOURCES,
  LINK_STATES,
  LINK_TITLE_SOURCES,
  type LinkCardsDto,
  type LinkPreviewDto,
} from './linkCards';
import { LINK_TYPES } from './persisted';
import { STATE_CATEGORIES, WORK_ITEM_TYPE_IDS } from './refs';

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

test('linkCards_ShouldTypeEveryFixture', () => {
  const cards = typed<LinkCardsDto>()(fixture.linkCards);
  const all = [...cards.code, ...cards.pages];
  expect(cards.workItems.length).toBeGreaterThan(0);
  for (const type of cards.workItems.map((row) => row.type)) expect(LINK_TYPES).toContain(type);
  for (const row of cards.workItems) {
    expect(WORK_ITEM_TYPE_IDS).toContain(row.itemType.id);
    expect(STATE_CATEGORIES).toContain(row.state.category);
  }
  for (const card of all) {
    expect(LINK_KINDS).toContain(card.kind);
    expect(LINK_GROUPS).toContain(card.group);
    expect(LINK_TITLE_SOURCES).toContain(card.titleSource);
    expect(LINK_STATE_SOURCES).toContain(card.stateSource);
    expect(LINK_PREVIEW_STATUSES).toContain(card.previewStatus);
    if (card.state !== null) expect(LINK_STATES).toContain(card.state);
  }
  expect(cards.code.every((card) => card.group === 'code' && card.kind !== 'page')).toBe(true);
  expect(cards.pages.every((card) => card.group === 'page' && card.kind === 'page')).toBe(true);
  expect(all.some((card) => card.refreshDue)).toBe(true);
});

test('linkPreview_ShouldTypeEveryFixture', () => {
  const previews = [
    typed<LinkPreviewDto>()(fixture.preview),
    typed<LinkPreviewDto>()(fixture.privatePreview),
  ];
  for (const preview of previews) {
    expect(LINK_KINDS).toContain(preview.kind);
    expect(preview.message.length).toBeGreaterThan(0);
  }
});
