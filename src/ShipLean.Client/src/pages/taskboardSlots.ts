import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { SprintHeaderProps } from '../features/sprintHeader/SprintHeader';
import type { TaskboardSettingsSlotProps } from '../slots/TaskboardSettingsSlot';
import type { TaskboardSlotProps } from '../slots/TaskboardSlot';
import { settingsDialogProps, type SetSearchParams } from './settingsDialogProps';

// One sprint's taskboard under the slim sprint header; its settings dialog's section is the query's ?settings=.
export function taskboardSlots(
  project: ProjectDto,
  sprint: SprintDto,
  sprints: SprintDto[],
  params: URLSearchParams,
  setParams: SetSearchParams,
): {
  header: SprintHeaderProps;
  taskboard: TaskboardSlotProps;
  taskboardSettings: TaskboardSettingsSlotProps;
} {
  return {
    header: { variant: 'slim', project, sprint, sprints, tab: 'taskboard' },
    taskboard: { project, sprint, sprints },
    taskboardSettings: {
      project,
      sprint,
      sprints,
      ...settingsDialogProps(params, setParams),
    },
  };
}
