import type { BoardLevel, BoardTypeStatesDto, BoardWarningDto } from '../../api/boards';
import type { BoardSettingsJson } from '../../api/persisted';
import type { PaneGroup } from '../../components/SettingsDialog/types';
import { AnnotationsPane } from './panes/AnnotationsPane';
import { CardReorderingPane } from './panes/CardReorderingPane';
import { ColumnsPane, columnsWarning } from './panes/ColumnsPane';
import { FieldsPane } from './panes/FieldsPane';
import { StylesPane } from './panes/StylesPane';
import { SwimlanesPane } from './panes/SwimlanesPane';
import { TagColoursPane } from './panes/TagColoursPane';

/** What every pane may read besides its draft; the Taskboard's is the same type with `level: 'taskboard'`. */
export interface BoardSettingsContext {
  projectId: string;
  boardId: string;
  level: BoardLevel;
  states: BoardTypeStatesDto[];
  warnings: BoardWarningDto[];
}

export type BoardPaneGroup = PaneGroup<BoardSettingsJson, BoardSettingsContext>;

/** The pane ids are the `?settings=` values. */
export const boardPaneIds = [
  'fields',
  'styles',
  'tags',
  'annotations',
  'columns',
  'swimlanes',
  'reordering',
] as const;
export type BoardPaneId = (typeof boardPaneIds)[number];

export const DEFAULT_BOARD_PANE: BoardPaneId = 'columns';

export const boardPaneGroups: BoardPaneGroup[] = [
  {
    label: 'Cards',
    panes: [
      { id: 'fields', label: 'Fields', sections: ['cardFields'], component: FieldsPane },
      { id: 'styles', label: 'Styles', sections: ['styleRules'], component: StylesPane },
      { id: 'tags', label: 'Tag colours', sections: ['tagColours'], component: TagColoursPane },
      {
        id: 'annotations',
        label: 'Annotations',
        sections: ['annotations'],
        component: AnnotationsPane,
      },
    ],
  },
  {
    label: 'Board',
    panes: [
      {
        id: 'columns',
        label: 'Columns',
        sections: ['columns'],
        component: ColumnsPane,
        warning: columnsWarning,
      },
      { id: 'swimlanes', label: 'Swimlanes', sections: ['swimlanes'], component: SwimlanesPane },
      {
        id: 'reordering',
        label: 'Card reordering',
        sections: ['cardReordering'],
        component: CardReorderingPane,
      },
    ],
  },
];
