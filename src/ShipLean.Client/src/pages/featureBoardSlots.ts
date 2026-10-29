import type { ProjectDto } from '../api/projects';
import type { FeatureBoardSettingsSlotProps } from '../slots/FeatureBoardSettingsSlot';
import type { FeatureBoardSlotProps } from '../slots/FeatureBoardSlot';
import { settingsDialogProps, type SetSearchParams } from './settingsDialogProps';

// One Feature's board; its settings dialog is the Taskboard's, its section the query's ?settings=.
export function featureBoardSlots(
  project: ProjectDto,
  itemKey: string,
  params: URLSearchParams,
  setParams: SetSearchParams,
): { featureBoard: FeatureBoardSlotProps; featureBoardSettings: FeatureBoardSettingsSlotProps } {
  return {
    featureBoard: { project, itemKey },
    featureBoardSettings: { project, itemKey, ...settingsDialogProps(params, setParams) },
  };
}
