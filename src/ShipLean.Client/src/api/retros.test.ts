import { expect, test } from 'vitest';
import sprintRetro from '../../../ShipLean.TestSupport/DtoFixtures/retros/sprint-retro.json';
import retroSearch from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-search.json';
import retroInsights from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-insights.json';
import retroThemes from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-themes.json';
import workItemRetros from '../../../ShipLean.TestSupport/DtoFixtures/retros/work-item-retros.json';
import retroFeatures from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-features.json';
import retroItem from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-item.json';
import actionDialog from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-dialog.json';
import actionDetail from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-detail.json';
import retroPoint from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-point.json';
import {
  RETRO_CATEGORIES,
  RETRO_COVERAGES,
  RETRO_ROLES,
  RETRO_THEME_MODES,
  RETRO_THEME_SET_BY,
  RETRO_THEME_STATUSES,
  RETRO_TONES,
  RETRO_VERDICTS,
  RETRO_DECISIONS,
  RETRO_FIX_TYPES,
  type RetroActionDetailDto,
  type RetroActionDialogDto,
  type RetroCardDto,
  type RetroFeaturesDto,
  type RetroItemDto,
  type RetroInsightsDto,
  type RetroPointDto,
  type RetroSearchDto,
  type RetroThemeListDto,
  type SprintRetroDto,
  type WorkItemRetrosDto,
} from './retros';

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

const cards = (sprintRetro as { cards: RetroCardDto[] }).cards;

test('retroFixtures_ShouldTypeEveryFixture', () => {
  const view = typed<SprintRetroDto>()(sprintRetro);
  typed<RetroSearchDto>()(retroSearch);
  typed<RetroInsightsDto>()(retroInsights);
  typed<RetroThemeListDto>()(retroThemes);
  typed<WorkItemRetrosDto>()(workItemRetros);
  typed<RetroPointDto>()(retroPoint);
  typed<RetroFeaturesDto>()(retroFeatures);
  typed<RetroItemDto>()(retroItem);
  typed<RetroActionDialogDto>()(actionDialog);
  typed<RetroActionDetailDto>()(actionDetail);
  expect(view.categories.map((column) => column.category)).toEqual([
    'wentWell',
    'notInPlan',
    'wentBadly',
    'improve',
  ]);
});

test('retroFixtures_ShouldUseEveryEnumValue', () => {
  const text = JSON.stringify([
    sprintRetro,
    retroSearch,
    retroInsights,
    retroPoint,
    workItemRetros,
    retroFeatures,
    retroItem,
    actionDialog,
    actionDetail,
  ]);
  const used = (key: string, values: readonly string[]) =>
    values.filter((value) => text.includes(`"${key}":"${value}"`));
  expect(used('category', RETRO_CATEGORIES)).toEqual([...RETRO_CATEGORIES]);
  expect(used('role', RETRO_ROLES)).toEqual([...RETRO_ROLES]);
  expect(used('themeSetBy', RETRO_THEME_SET_BY)).toEqual([...RETRO_THEME_SET_BY]);
  expect(used('tone', RETRO_TONES)).toEqual([...RETRO_TONES]);
  expect(used('status', RETRO_THEME_STATUSES)).toEqual([...RETRO_THEME_STATUSES]);
  expect(used('status', RETRO_COVERAGES)).toEqual(expect.arrayContaining([...RETRO_COVERAGES]));
  expect(used('verdict', RETRO_VERDICTS)).toEqual([...RETRO_VERDICTS]);
  expect(used('decision', RETRO_DECISIONS)).toEqual([...RETRO_DECISIONS]);
  expect(used('fixType', RETRO_FIX_TYPES)).toEqual(expect.arrayContaining(['skill']));
  expect(RETRO_THEME_MODES).toContain(retroThemes.mode);
});

test('sprintRetro_ShouldPinTheSpecialCases', () => {
  expect(
    (sprintRetro as SprintRetroDto).categories
      .flatMap((column) => column.groups)
      .some((group) => group.theme === null),
  ).toBe(true);
  expect(
    cards.some((card) => card.compiledFrom.length > 0 && card.workItem?.type.id === 'feature'),
  ).toBe(true);
  expect(cards.some((card) => card.workItem === null && card.author.role === 'person')).toBe(true);
  expect(cards.some((card) => card.points.some((point) => point.action !== null))).toBe(true);
  expect(cards.some((card) => card.partial)).toBe(true);
});

test('retroCategories_ShouldRunInDisplayOrder', () => {
  expect([...RETRO_CATEGORIES]).toEqual(['wentWell', 'notInPlan', 'wentBadly', 'improve']);
});
