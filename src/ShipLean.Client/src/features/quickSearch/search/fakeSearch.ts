import type { QuickSearchGroup } from '../types';

// TODO: replaced by GET /api/search results in useSearchSource.
export const fakeSearchGroups: readonly QuickSearchGroup[] = [
  {
    id: 'items',
    title: 'Work items',
    rows: [
      {
        kind: 'item',
        id: 'SHIP-41',
        item: {
          key: 'SHIP-41',
          title: 'Proxy sign-in for reverse-proxy installs',
          type: 'feature',
          state: 'Active',
          stateCategory: 'inProgress',
          parentPath: ['SHIP-12'],
          projectKey: 'SHIP',
        },
        match: null,
      },
      {
        kind: 'item',
        id: 'MOB-17',
        item: {
          key: 'MOB-17',
          title: 'Sign in with Apple',
          type: 'story',
          state: 'New',
          stateCategory: 'proposed',
          parentPath: [],
          projectKey: 'MOB',
        },
        match: { in: 'comment', snippet: 'Apple wants the sign-off before we ship.' },
      },
    ],
  },
  {
    id: 'sprints',
    title: 'Sprints',
    rows: [
      {
        kind: 'sprint',
        id: '/p/SHIP/sprints/24',
        name: 'Sprint 24',
        goal: 'Sign-in hardening',
        datesLabel: '14 – 27 Oct',
        current: true,
        projectKey: 'SHIP',
        open: () => undefined,
      },
    ],
  },
  {
    id: 'projects',
    title: 'Projects',
    rows: [
      {
        kind: 'project',
        id: 'DS',
        key: 'DS',
        name: 'Digital signage',
        detail: '2 members',
        current: false,
        open: () => undefined,
      },
    ],
  },
];
