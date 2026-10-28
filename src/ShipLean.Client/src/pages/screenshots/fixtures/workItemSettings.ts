import type { BoardSettingsDto } from '../../../api/boards';
import type { ProjectSettingsDto, StateDto } from '../../../api/projects';
import type { StateCategory, WorkItemTypeId } from '../../../api/refs';
import { emptySettings } from '../../../features/boardSettings/ct/boardSettingsFixtures';
import { SHELL_MEMBERS, SHELL_PROJECT } from '../shellStub';

type Row = [name: string, category: StateCategory, itemCount?: number];

const states = (type: WorkItemTypeId, rows: Row[]): StateDto[] =>
  rows.map(([name, category, itemCount = 0], i) => ({
    id: `${type}-${name.toLowerCase().replace(/\W+/g, '-')}`,
    type,
    name,
    category,
    order: i + 1,
    itemCount,
    startsAgent: false,
  }));

const simple = (): Row[] => [
  ['New', 'proposed'],
  ['Active', 'inProgress'],
  ['Resolved', 'resolved'],
  ['Closed', 'completed'],
  ['Removed', 'removed'],
];

// design/screens/settings.html: Bug has six states (Triaged is custom), 4 items, two of them in New and two Closed.
export const projectSettings: ProjectSettingsDto = {
  general: {
    key: SHELL_PROJECT.key,
    name: SHELL_PROJECT.name,
    description: '',
    workingDays: SHELL_PROJECT.workingDays,
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    agentConnections: [],
  },
  members: SHELL_MEMBERS,
  sprints: { velocityWindow: 3, velocityOverride: null },
  states: [
    ...states('epic', simple()),
    ...states('feature', simple()),
    ...states('story', simple()),
    ...states('bug', [
      ['New', 'proposed', 2],
      ['Triaged', 'proposed'],
      ['Active', 'inProgress'],
      ['Resolved', 'resolved'],
      ['Closed', 'completed', 2],
      ['Removed', 'removed'],
    ]),
    ...states('task', [
      ['To Do', 'proposed'],
      ['Doing', 'inProgress'],
      ['In review', 'resolved'],
      ['Done', 'completed'],
      ['Blocked', 'inProgress'],
      ['Removed', 'removed'],
    ]),
  ],
};

const column = (id: string, name: string, bugStates: string[]) => ({
  id,
  name,
  wipLimit: null,
  split: false,
  definitionOfDone: '',
  stateMappings: bugStates.map((stateId) => ({ type: 'bug' as const, stateId })),
});

export const storiesBoardSettings: BoardSettingsDto = {
  id: 'b-stories',
  projectId: SHELL_PROJECT.id,
  level: 'stories',
  settings: {
    ...emptySettings(),
    columns: [
      column('c1', 'New', ['bug-new']),
      column('c2', 'Ready', ['bug-triaged']),
      column('c3', 'In progress', ['bug-active']),
      column('c4', 'In review', ['bug-resolved']),
      column('c5', 'Done', ['bug-closed']),
      column('c6', 'Hidden (Removed)', ['bug-removed']),
    ],
  },
  updatedAt: '',
  states: [],
  warnings: [],
};
