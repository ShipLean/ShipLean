import { expect } from '@playwright/experimental-ct-react';
import { backlogView, ROWS } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds } from '../../features/backlog/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// The Stories backlog's List mode on a phone (SHIP-716 D1): two-line touch rows in place of the table.

phonePageShots([
  {
    name: 'backlogs-rows',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView(ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length),
  },
]);
