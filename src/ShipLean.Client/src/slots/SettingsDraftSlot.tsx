import type { ReactNode } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { ProjectSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { SettingsDraftFrame } from '../features/settings/draft/SettingsDraftFrame';

// The frame of every project settings tab: the tab list with dirty dots, the one Save and Discard header, Save
// jumping to the first error. `states`, `integrations` and `demos` are the content of those tabs; the frame shows each on
// its tab. Demos, Inspection and Prompts have their own Save and join no draft.
export interface SettingsDraftSlotProps {
  project: ProjectDto;
  /** Undefined on the tab-less address: the pane index at touch. */
  tab: ProjectSettingsTab | undefined;
  states: ReactNode;
  integrations: ReactNode;
  demos: ReactNode;
  inspection?: ReactNode;
  prompts?: ReactNode;
}

export function SettingsDraftSlot(props: SettingsDraftSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSettingsDraft} className="h-full">
      <SettingsDraftFrame {...props} />
    </div>
  );
}
