import type { StateDto, MemberDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import type { UserRef } from '../../../api/refs';
import type { WorkItemDto } from '../../../api/workItems';

export const priya: UserRef = {
  id: 'u-priya',
  name: 'Priya Nair',
  initials: 'PN',
  avatarColour: '#5e81ac',
  photoUrl: null,
};
export const mei: UserRef = {
  id: 'u-mei',
  name: 'Mei Lin',
  initials: 'ML',
  avatarColour: '#a3be8c',
  photoUrl: null,
};

export const story: WorkItemDto = {
  id: 'w141',
  projectId: 'p1',
  key: 'SHIP-141',
  number: 141,
  type: { id: 'story', name: 'User Story' },
  title: 'Pay with saved card',
  description: 'Let **returning** customers pay.',
  descriptionHtml: '<p>Let <strong>returning</strong> customers pay.</p>',
  acceptanceCriteria: [
    { text: 'Card is tokenised', done: true },
    { text: 'Declines are explained', done: false },
  ],
  reproSteps: '',
  reproStepsHtml: '',
  severity: null,
  state: { id: 's-active', name: 'Active', category: 'inProgress' },
  assignee: priya,
  createdBy: priya,
  changedBy: mei,
  sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
  priority: 2,
  points: 5,
  parent: null,
  tags: ['Payments'],
  position: { columnId: null, done: false, laneId: null, columnOrder: null },
  backlogOrder: 'a0',
  links: [],
  children: [],
  originalSystem: null,
  revision: 7,
  createdAt: '2026-10-01T09:00:00+00:00',
  createdAtLabel: '1 Oct',
  changedAt: '2026-10-20T09:00:00+00:00',
  changedAtLabel: '20 Oct',
  goal: '',
  statusLine: '',
  statusVerified: null,
  slug: null,
  startedOn: null,
  startedLabel: null,
};

export const bug: WorkItemDto = {
  ...story,
  id: 'w153',
  key: 'SHIP-153',
  number: 153,
  type: { id: 'bug', name: 'Bug' },
  title: 'Crash when biometric prompt is cancelled',
  acceptanceCriteria: [],
  reproSteps: '1. Open the prompt\n2. Cancel',
  reproStepsHtml: '<ol><li>Open the prompt</li><li>Cancel</li></ol>',
  severity: 2,
  state: { id: 's-triaged', name: 'Triaged', category: 'proposed' },
};

export const states: StateDto[] = [
  {
    id: 's-new',
    type: 'story',
    name: 'New',
    category: 'proposed',
    order: 1,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's-active',
    type: 'story',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 1,
    startsAgent: false,
  },
  {
    id: 's-triaged',
    type: 'bug',
    name: 'Triaged',
    category: 'proposed',
    order: 1,
    itemCount: 1,
    startsAgent: false,
  },
  {
    id: 's-t-new',
    type: 'task',
    name: 'To Do',
    category: 'proposed',
    order: 1,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's-t-active',
    type: 'task',
    name: 'In Progress',
    category: 'inProgress',
    order: 2,
    itemCount: 1,
    startsAgent: false,
  },
  {
    id: 's-t-done',
    type: 'task',
    name: 'Done',
    category: 'completed',
    order: 3,
    itemCount: 1,
    startsAgent: false,
  },
];

export const members: MemberDto[] = [
  { user: priya, email: 'priya@example.com', role: 'member', status: 'active' },
  { user: mei, email: 'mei@example.com', role: 'admin', status: 'active' },
];

export const sprints: SprintDto[] = [
  {
    id: 'sp24',
    name: 'Sprint 24',
    startDate: '2026-10-19',
    endDate: '2026-10-30',
    goal: '',
    status: 'current',
    rolloverStatus: null,
    datesLabel: '19 Oct – 30 Oct',
    timeLabel: '5 days left',
  },
];
