import type { CommentDto } from '../../../api/comments';
import type { StateDto } from '../../../api/projects';
import type { LinkDto, WorkItemDto } from '../../../api/workItems';
import { story } from '../../../features/workItem/ct/fixtures';
import { attachment, change, revision } from '../../../features/workItem/ct/stubSide';
import { PEOPLE, SHELL_SPRINTS } from '../shellStub';

// SHIP-141 and SHIP-153 as design/screens/work-item.html draws them, for the shell's people and sprints.
const sprint24 = SHELL_SPRINTS.find((s) => s.name === 'Sprint 24');

const link = (id: string, type: LinkDto['type'], key: string | null, title: string, url?: string) =>
  ({
    id,
    type,
    workItemId: key,
    key,
    title,
    url: url ?? null,
    card: null,
    autoLinked: null,
  }) satisfies LinkDto;

const task = (n: number, title: string, state: string, category: 'proposed' | 'inProgress') => ({
  id: `t${n}`,
  key: `SHIP-${n}`,
  type: { id: 'task' as const, name: 'Task' },
  title,
  state: { id: `s-t-${state}`, name: state, category },
  assignee: null,
  points: null,
});

export const savedCardStory: WorkItemDto = {
  ...story,
  title: 'Pay with saved card',
  description:
    "As a returning customer, I want to pay with a card I saved before, so I don't have to type the number again.\n\nCards live in the **vault service** as tokens; we never see the card number. Show the brand, the last four digits and the expiry from `GET /cards`.\n\n- Default to the card used last\n- Expired cards are shown but can't be picked\n- Fall back to the new-card form if the vault is down",
  descriptionHtml:
    "<p>As a returning customer, I want to pay with a card I saved before, so I don't have to type the number again.</p><p>Cards live in the <strong>vault service</strong> as tokens; we never see the card number. Show the brand, the last four digits and the expiry from <code>GET /cards</code>.</p><ul><li>Default to the card used last</li><li>Expired cards are shown but can't be picked</li><li>Fall back to the new-card form if the vault is down</li></ul>",
  acceptanceCriteria: [
    { text: 'Saved cards show brand, last four digits and expiry', done: true },
    { text: 'The card used last is selected by default', done: true },
    { text: "Expired cards are greyed out and can't be selected", done: false },
    {
      text: 'Paying with a saved card skips the card form and goes straight to Review',
      done: false,
    },
    { text: 'If the vault is unavailable, the new-card form shows with a notice', done: false },
  ],
  assignee: PEOPLE.priya,
  createdBy: PEOPLE.ana,
  changedBy: PEOPLE.priya,
  sprint: sprint24 ? { id: sprint24.id, name: sprint24.name, status: 'current' } : null,
  parent: {
    id: 'w100',
    key: 'SHIP-100',
    type: { id: 'feature', name: 'Feature' },
    title: 'Checkout v2',
  },
  tags: ['Checkout', 'PCI'],
  links: [
    link('l1', 'predecessor', 'SHIP-120', 'Guest checkout'),
    link('l2', 'successor', 'SHIP-158', 'Gift card balance check'),
    link('l3', 'related', 'SHIP-144', 'Address autocomplete'),
    link('l4', 'url', null, 'Card vault API notes', 'https://wiki.example.com/card-vault'),
  ],
  children: [
    {
      ...task(171, 'API: tokenised card lookup', 'In review', 'inProgress'),
      assignee: PEOPLE.priya,
    },
    { ...task(172, 'UI: saved card picker', 'Active', 'inProgress'), assignee: PEOPLE.mei },
    task(173, 'E2E test', 'New', 'proposed'),
  ],
  revision: 6,
  createdAtLabel: 'Fri 9 Oct',
  changedAtLabel: 'Mon 19 Oct, 09:12',
};

export const crashBug: WorkItemDto = {
  ...savedCardStory,
  id: 'w153',
  key: 'SHIP-153',
  number: 153,
  type: { id: 'bug', name: 'Bug' },
  title: 'Crash when biometric prompt is cancelled',
  description: 'The app closes when the biometric prompt is dismissed during a saved-card payment.',
  descriptionHtml:
    '<p>The app closes when the biometric prompt is dismissed during a saved-card payment.</p>',
  acceptanceCriteria: [],
  reproSteps:
    '1. Open the app on Android 14\n2. Start a saved-card payment\n3. Cancel the biometric prompt',
  reproStepsHtml:
    '<ol><li>Open the app on Android 14</li><li>Start a saved-card payment</li><li>Cancel the biometric prompt</li></ol>',
  severity: 2,
  state: { id: 's-b-triaged', name: 'Triaged', category: 'proposed' },
  assignee: PEOPLE.sam,
  tags: ['Mobile', 'Payments'],
  links: [link('l1', 'blockedBy', 'SHIP-141', 'Pay with saved card')],
  children: [],
};

const stateRow = (id: string, type: StateDto['type'], name: string, order: number): StateDto => ({
  id,
  type,
  name,
  category: order === 1 ? 'proposed' : order === 2 ? 'inProgress' : 'completed',
  order,
  itemCount: 0,
  startsAgent: false,
});
export const workItemStates: StateDto[] = [
  stateRow('s-new', 'story', 'New', 1),
  stateRow('s-active', 'story', 'Active', 2),
  stateRow('s-b-triaged', 'bug', 'Triaged', 1),
  stateRow('s-t-New', 'task', 'New', 1),
  stateRow('s-t-Active', 'task', 'Active', 2),
  { ...stateRow('s-t-In review', 'task', 'In review', 3), category: 'inProgress' },
];

const comment = (
  id: string,
  author: CommentDto['author'],
  when: string,
  body: string,
  html: string,
) =>
  ({
    id,
    workItemId: 'w141',
    author,
    body,
    bodyHtml: html,
    createdAt: '2026-10-22T10:14:00+00:00',
    createdLabel: when,
    editedAt: null,
    editedLabel: null,
    agentName: null,
    superseded: null,
  }) satisfies CommentDto;

export const comments: CommentDto[] = [
  comment(
    'c1',
    PEOPLE.tom,
    'Thu 22 Oct, 10:14',
    'Card vault API is on staging now.',
    '<p>Card vault API is on staging now. <code>brand</code>, <code>last4</code> and <code>expiry</code>, nothing else.</p>',
  ),
  comment(
    'c2',
    PEOPLE.mei,
    'Fri 23 Oct, 16:02',
    'Picker design is ready for review.',
    '<p>Picker design is ready for review.</p>',
  ),
  comment(
    'c3',
    PEOPLE.priya,
    'Sat 24 Oct, 09:30',
    'Thanks, picking this up Monday.',
    '<p>Thanks, picking this up Monday.</p>',
  ),
];

export const attachments = [
  {
    ...attachment('a1', 'saved-card-picker.png', PEOPLE.mei, 412 * 1024),
    createdLabel: 'Fri 16 Oct',
  },
  {
    ...attachment('a2', 'vault-api-contract.pdf', PEOPLE.tom, 1.2 * 1024 * 1024),
    createdLabel: 'Tue 13 Oct',
  },
];

export const revisions = [
  revision(
    6,
    PEOPLE.priya,
    [
      change('state', 's-new', 's-active', ['New', 'Active']),
      change('assignee', null, 'u-priya', ['Unassigned', 'Priya Shah']),
    ],
    { changedAtLabel: 'Mon 19 Oct, 09:12' },
  ),
  revision(5, PEOPLE.tom, [change('sprint', null, 'sprint-24', ['Sprint Backlog', 'Sprint 24'])], {
    changedAtLabel: 'Fri 16 Oct, 15:40',
  }),
  revision(4, PEOPLE.mei, [change('points', 3, 5), change('tags', null, 'PCI', ['none', 'PCI'])], {
    changedAtLabel: 'Thu 15 Oct, 11:02',
  }),
  revision(3, PEOPLE.priya, [change('parent', null, 'w100', ['none', 'Checkout v2'])], {
    changedAtLabel: 'Tue 13 Oct, 10:30',
  }),
  revision(2, PEOPLE.ana, [change('priority', 3, 2)], { changedAtLabel: 'Mon 12 Oct, 16:15' }),
];
