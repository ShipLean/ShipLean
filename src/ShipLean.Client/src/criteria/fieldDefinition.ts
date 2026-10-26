import { FIELD_IDS, type FieldDefinition, type FieldId } from '../api/persisted';

/** The `@me` placeholder a user clause may hold; it stands for the signed-in user. */
export const ME = '@me';

const definitions: readonly FieldDefinition[] = FIELD_IDS;

/** The definition of a field the criteria can use, or undefined for an unknown field or one that is not for criteria. */
export function criteriaField(id: string): FieldDefinition | undefined {
  return definitions.find((f) => f.id === id && f.usedIn.includes('criteria'));
}

/** Every field a clause can filter on, in the canonical field order. */
export const CRITERIA_FIELDS: readonly FieldId[] = FIELD_IDS.filter((f) =>
  (f.usedIn as readonly string[]).includes('criteria'),
).map((f) => f.id);
