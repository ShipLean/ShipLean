import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../ct/fixtures';
import { FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { LevelHarness } from '../ct/LevelHarness';
import { stubLevel } from '../ct/stubLevels';
import { ToolbarHarness } from '../ct/ToolbarHarness';
import { backlogTestIds as ids } from '../testIds';

test('TriageChip_WhenCount_ShouldLinkToFilter', async ({ mount, page }) => {
  await mount(<ToolbarHarness triageCount={3} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  const chip = page.getByTestId(ids.triageChip);
  await expect(chip).toHaveText('3 in triage');

  await chip.click();
  await expect(page.getByTestId('harness-state')).toContainText('triage-shown');
});

test('TriageChip_WhenNoCount_ShouldNotShow', async ({ mount, page }) => {
  await mount(<ToolbarHarness triageCount={0} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.triageChip)).toHaveCount(0);
});

// The real wiring: the Features backlog page reads triageStateIds, the chip click sets the criteria, and the next read asks for them.
test('TriageChip_WhenClicked_ShouldReadTheBacklogFilteredToTheIntakeStates', async ({
  mount,
  page,
}) => {
  const intake = ['00000003-0000-4000-8001-0000000000a1', '00000003-0000-4000-8001-0000000000a2'];
  const server = await stubLevel(page, {
    ...levelView('features', FEATURE_ROWS),
    triageCount: 2,
    triageStateIds: intake,
  });
  await mount(<LevelHarness project={PROJECT} level="features" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/features' },
  });
  await expect(page.getByTestId(ids.triageChip)).toHaveText('2 in triage');
  expect(new URL(server.reads[0], 'http://x').searchParams.get('filter')).toBeFalsy();

  await page.getByTestId(ids.triageChip).click();

  await expect
    .poll(() => {
      const filter = new URL(server.reads[server.reads.length - 1], 'http://x').searchParams.get(
        'filter',
      );
      return filter ? JSON.parse(filter) : null;
    })
    .toEqual({ match: 'all', clauses: [{ field: 'state', op: 'in', value: intake }] });
});
