import type { ProjectDto } from '../api/projects';
import type { Level } from '../routes';
import type { BoardSettingsSlotProps } from '../slots/BoardSettingsSlot';
import type { BoardSlotProps } from '../slots/BoardSlot';
import { settingsDialogProps, type SetSearchParams } from './settingsDialogProps';

export interface BoardSlots {
  board: BoardSlotProps;
  boardSettings: BoardSettingsSlotProps;
}

// A level's board and its settings dialog, whose section is the query's ?settings=.
export function boardSlots(
  project: ProjectDto,
  level: Level,
  params: URLSearchParams,
  setParams: SetSearchParams,
): BoardSlots {
  return {
    board: { project, level },
    boardSettings: { project, level, ...settingsDialogProps(params, setParams) },
  };
}
