import { expect, test } from '@playwright/experimental-ct-react';
import { AsksHarness } from './ct/AsksHarness';
import { RegionHarness } from './ct/RegionHarness';
import {
  answered,
  empty,
  feature,
  featureChildrenOnly,
  folded,
  other,
  unassigned,
  waiting,
} from './ct/fixtures';
import { stubAsks } from './ct/stubAsks';
import { AsksHeader } from './AsksHeader';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};
const section = <AsksHarness projectKey="SHIP" item={item} />;

test('AsksHeader_WhenWaitingOnMe_ShouldSayWaitingOnYou', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AsksHeader item={item} asks={waiting} />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.waitingOn)).toContainText('Waiting on you');
  await expect(component.getByTestId(ids.count)).toHaveText('5');
  await expect(component.getByTestId(ids.summary)).toHaveText(
    '1 question · 4 decisions going with recommended',
  );
});

test('AsksHeader_WhenWaitingOnOther_ShouldNameOwnerAndSayYouCanAnswer', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AsksHeader item={item} asks={other} />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.waitingOn)).toContainText('Waiting on Priya Shah');
  await expect(component.getByTestId(ids.summary)).toContainText('you can answer too');
});

test('AsksHeader_WhenUnassigned_ShouldNoteCreator', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AsksHeader item={item} asks={unassigned} />
    </RegionHarness>,
  );

  const chip = component.getByTestId(ids.waitingOn);
  await expect(chip).toContainText('Waiting on Ana Costa');
  await expect(chip).toHaveAttribute(
    'title',
    "The item's owner: its creator; nobody is assigned. Anyone who can edit SHIP-141 can answer.",
  );
});

test('AsksHeader_WhenOnlyChildrenWait_ShouldPointToStoriesBelow', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AsksHeader
        item={{ ...item, type: { id: 'feature', name: 'Feature' } }}
        asks={featureChildrenOnly}
      />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.summary)).toHaveText(
    'Nothing here waits. 7 asks on 2 stories below',
  );
  await expect(component.getByTestId(ids.waitingOn)).toHaveCount(0);
  await expect(component.getByTestId(ids.count)).toHaveCount(0);
});

test('AsksHeader_WhenFolded_ShouldHideBulkConfirm', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AsksHeader item={item} asks={waiting} folded />
      <AsksHeader item={item} asks={waiting} />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.bulkConfirm)).toHaveCount(1);
});

test('DecisionsSection_WhenNothingWaiting_ShouldFoldToSummaryAndUnfold', async ({
  mount,
  page,
}) => {
  await stubAsks(page, folded);
  const component = await mount(section);

  await expect(component.getByTestId(ids.summary)).toHaveText(
    '✓ Nothing waiting 7 decisions: 5 confirmed, 1 changed, 1 withdrawn · 2 questions answered',
  );
  await expect(component.getByTestId(ids.questionRow)).toHaveCount(0);
  const toggle = component.getByTestId(ids.foldToggle);
  await expect(toggle).toHaveText('▸');

  await toggle.click();

  await expect(component.getByTestId(ids.questionRow)).toHaveCount(2);
  await expect(toggle).toHaveText('▾');
});

test('DecisionsSection_WhenEmpty_ShouldShowEmptyLine', async ({ mount, page }) => {
  await stubAsks(page, empty);
  const component = await mount(section);

  await expect(component.getByTestId(ids.summary)).toHaveText('No decisions or questions yet.');
  const hint = component.getByTestId(ids.empty);
  await expect(hint).toHaveText(
    'When an agent records a decision with record_decisions or asks with ask, it shows here for the owner to answer.',
  );
  await expect(component.getByTestId(ids.foldToggle)).toHaveCount(0);
});

test('DecisionsSection_WhenViewport900_ShouldNotScrollSideways', async ({ mount, page }) => {
  await page.setViewportSize({ width: 900, height: 800 });
  await stubAsks(page, waiting);
  const component = await mount(section);
  await component.getByTestId(ids.questionReply).first().click();
  await expect(component.getByTestId(ids.questionReplyInput)).toBeVisible();

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

const shots = [
  ['waiting', waiting],
  ['other', other],
  ['unassigned', unassigned],
  ['answered', answered],
  ['folded', folded],
  ['empty', empty],
  ['reply', waiting],
  ['feature', feature],
] as const;

for (const [name, asks] of shots) {
  for (const theme of ['light', 'dark'] as const) {
    test(`DecisionsSection_WhenRendered_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await stubAsks(page, asks);
      const component = await mount(
        <AsksHarness
          projectKey="SHIP"
          item={
            name === 'feature'
              ? {
                  ...item,
                  id: feature.workItemId,
                  key: 'SHIP-101',
                  type: { id: 'feature', name: 'Feature' },
                }
              : item
          }
        />,
      );
      const root = component.getByTestId(ids.section);
      if (name === 'answered') await component.getByTestId(ids.foldToggle).click();
      if (name === 'reply') {
        await component.getByTestId(ids.questionReply).click();
        await component
          .getByTestId(ids.questionReplyInput)
          .fill('Wait for !57. Do not load test against the real vault.');
      }
      await expect(root).toBeVisible();
      await expect(root).toHaveScreenshot(`asks-${name}.${theme}.png`, { animations: 'disabled' });
    });
  }
}
