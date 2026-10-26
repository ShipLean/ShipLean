import {
  FIELD_IDS,
  type BoardSettingsJson,
  type CardFieldSettingJson,
  type FieldDefinition,
} from '../../../../api/persisted';
import type { WorkItemTypeId } from '../../../../api/refs';

const definitions: readonly FieldDefinition[] = FIELD_IDS;

/** Fields the board never draws as extra rows: the parent line is always on the card, the column says the state. */
export const ALWAYS_DRAWN_FIELDS: Record<string, string> = {
  parent: 'always on the card',
  state: 'shown by the column',
};

/** The most extra fields one type's card shows; the server refuses more. */
export const MAX_EXTRA_FIELDS = 10;

export type CoreField = 'showKey' | 'showAssignee' | 'showTags' | 'showPoints' | 'showEmptyFields';

/** The extra fields a card of this type can show, in the canonical field order. */
export function extraFieldsFor(type: WorkItemTypeId) {
  return definitions.filter((f) => f.usedIn.includes('cardField') && f.appliesTo.includes(type));
}

/** The label of a field id; an id the vocabulary does not know shows as itself. */
export function fieldLabel(id: string): string {
  return definitions.find((f) => f.id === id)?.label ?? id;
}

/** Fields the type could still add: not yet listed. */
export function addableFields(setting: CardFieldSettingJson) {
  return extraFieldsFor(setting.type).filter(
    (f) => !setting.extraFields.includes(f.id) && !(f.id in ALWAYS_DRAWN_FIELDS),
  );
}

export function isFull(setting: CardFieldSettingJson): boolean {
  return setting.extraFields.length >= MAX_EXTRA_FIELDS;
}

export function isOverLimit(setting: CardFieldSettingJson): boolean {
  return setting.extraFields.length > MAX_EXTRA_FIELDS;
}

/** The settings with one type's setting replaced. */
export function withSetting(
  draft: BoardSettingsJson,
  next: CardFieldSettingJson,
): BoardSettingsJson {
  return {
    ...draft,
    cardFields: draft.cardFields.map((s) => (s.type === next.type ? next : s)),
  };
}

/** Appends a field; a full list, a field already listed or an unknown field is refused (returns the setting unchanged). */
export function addExtra(setting: CardFieldSettingJson, id: string): CardFieldSettingJson {
  if (isFull(setting) || setting.extraFields.includes(id)) return setting;
  return { ...setting, extraFields: [...setting.extraFields, id] };
}

export function removeExtra(setting: CardFieldSettingJson, id: string): CardFieldSettingJson {
  return { ...setting, extraFields: setting.extraFields.filter((f) => f !== id) };
}

/** Swaps the field at `index` with the one above it. */
export function moveExtraUp(setting: CardFieldSettingJson, index: number): CardFieldSettingJson {
  if (index <= 0 || index >= setting.extraFields.length) return setting;
  const next = [...setting.extraFields];
  [next[index - 1], next[index]] = [next[index], next[index - 1]];
  return { ...setting, extraFields: next };
}
