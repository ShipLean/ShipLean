import { fieldLabels, type DraftField } from '../form/draft';

/** The changed fields by name, in form order, as the Save bar and the header's save state list them. */
export const changedFieldNames = (changed: readonly DraftField[]): string =>
  changed.map((field) => fieldLabels[field]).join(', ');
