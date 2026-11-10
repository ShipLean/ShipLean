// Board settings shapes. C#: ShipLean.Contracts.Boards. Nothing is omitted on the wire: absent values are null and lists are [].
import type { BoardSettingsJson } from './persisted';
import type { StateRef, WorkItemTypeId } from './refs';

export const BOARD_LEVELS = ['stories', 'features', 'epics', 'taskboard'] as const;
export type BoardLevel = (typeof BOARD_LEVELS)[number];

export const BOARD_WARNING_KINDS = ['unmappedState'] as const;
export type BoardWarningKind = (typeof BOARD_WARNING_KINDS)[number];

/** The top-level properties of BoardSettingsJson: the names in BoardSettingsPutResult.mergedSections. */
export const BOARD_SECTIONS = [
  'columns',
  'swimlanes',
  'cardFields',
  'styleRules',
  'tagColours',
  'annotations',
  'cardReordering',
  'transitions',
] as const satisfies readonly (keyof BoardSettingsJson)[];
export type BoardSection = (typeof BOARD_SECTIONS)[number];

export interface BoardWarningDto {
  kind: BoardWarningKind;
  type: WorkItemTypeId;
  state: StateRef;
  message: string;
}

/** The project's states of one type in state order, Removed included. */
export interface BoardTypeStatesDto {
  type: WorkItemTypeId;
  states: StateRef[];
}

export interface BoardSettingsDto {
  id: string;
  projectId: string;
  level: BoardLevel;
  settings: BoardSettingsJson;
  updatedAt: string;
  states: BoardTypeStatesDto[];
  warnings: BoardWarningDto[];
}

/** basedOn is the document the caller loaded; settings is the edited copy. */
export interface BoardSettingsPut {
  basedOn: BoardSettingsJson;
  settings: BoardSettingsJson;
}

export interface BoardSettingsPutResult {
  board: BoardSettingsDto;
  mergedSections: BoardSection[];
}

export interface BoardSummaryDto {
  id: string;
  level: BoardLevel;
  name: string;
}
