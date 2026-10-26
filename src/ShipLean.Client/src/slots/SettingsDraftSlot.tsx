import type { ReactNode } from 'react';
import type { ProjectDto } from '../api/projects';
import type { ProjectSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { SettingsDraftFrame } from '../features/settings/draft/SettingsDraftFrame';

// The frame of every project settings tab: the tab list with dirty dots, the one Save and Discard header, Save
// jumping to the first error. `states` and `integrations` are the content of those two tabs; the frame shows each on
// its tab.
export interface SettingsDraftSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
  states: ReactNode;
  integrations: ReactNode;
}

export function SettingsDraftSlot(props: SettingsDraftSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSettingsDraft} className="h-full">
      <SettingsDraftFrame {...props} />
    </div>
  );
}
