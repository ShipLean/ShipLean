import type { BoardSettingsJson } from '../../api/persisted';
import type { BoardPaneGroup } from '../boardSettings/panes';
import { FieldsPane } from './panes/FieldsPane';
import { StylesPane } from './panes/StylesPane';
import { TagColoursPane } from './panes/TagColoursPane';

export type TaskboardPaneId = 'fields' | 'styles' | 'tags' | 'columns';

export const DEFAULT_TASKBOARD_PANE: TaskboardPaneId = 'fields';

export const taskboardPaneGroups: BoardPaneGroup[] = [
  {
    label: 'Cards',
    panes: [
      {
        id: 'fields',
        label: 'Fields',
        sections: ['cardFields', 'annotations'],
        component: FieldsPane,
      },
      { id: 'styles', label: 'Styles', sections: ['styleRules'], component: StylesPane },
      { id: 'tags', label: 'Tag colours', sections: ['tagColours'], component: TagColoursPane },
    ],
  },
  {
    label: 'Board',
    panes: [
      {
        id: 'columns',
        label: 'Columns',
        sections: ['columns'] satisfies (keyof BoardSettingsJson)[],
        disabled:
          'The Taskboard columns are the Task states: set them in Project settings, Task states.',
      },
    ],
  },
];
