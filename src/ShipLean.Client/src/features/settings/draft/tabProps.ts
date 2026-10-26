import type { Dispatch, SetStateAction } from 'react';
import type { FieldError, SettingsDraft } from './model';

/** What every tab gets: the one draft, whether it may be edited, and the errors Save found. */
export interface TabProps {
  draft: SettingsDraft;
  setDraft: Dispatch<SetStateAction<SettingsDraft>>;
  readOnly: boolean;
  errors: FieldError[];
}
