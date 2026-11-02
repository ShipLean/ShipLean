import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT } from '../ct/fixtures';
import { backlogTestIds as ids } from '../testIds';
import { stubNewFeature } from './ct/stubNewFeature';
import { NewFeatureDialog } from './NewFeatureDialog';

const hooks = { hooksConfig: { route: '/p/SHIP/backlogs/features' } };

test('NewFeatureDialog_WhenTitleTyped_ShouldSuggestSlug', async ({ mount, page }) => {
  const server = await stubNewFeature(page);
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);

  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');

  await expect(page.getByTestId(ids.newFeatureSlug)).toHaveValue('express-checkout');
  await expect(page.getByTestId(ids.newFeatureSlugNote)).toHaveAttribute('data-available', 'true');
  expect(server.checks).toContain('express-checkout');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeEnabled();
});

test('NewFeatureDialog_WhenSlugNoteAppears_ShouldNotMoveTheFieldsBelow', async ({
  mount,
  page,
}) => {
  await stubNewFeature(page);
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);
  const goal = page.getByTestId(ids.newFeatureGoal);
  const before = await goal.boundingBox();

  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');
  await expect(page.getByTestId(ids.newFeatureSlugNote)).toHaveAttribute('data-available', 'true');

  expect((await goal.boundingBox())?.y).toBe(before?.y);
});

test('NewFeatureDialog_WhenSlugTaken_ShouldDisableCreate', async ({ mount, page }) => {
  await stubNewFeature(page, { taken: ['express-checkout'] });
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);

  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');

  await expect(page.getByTestId(ids.newFeatureSlugNote)).toHaveAttribute('data-available', 'false');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeDisabled();
  await page.getByTestId(ids.newFeatureSlug).fill('express-checkout-2');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeEnabled();
});

test('NewFeatureDialog_WhenSlugEdited_ShouldStopSuggesting', async ({ mount, page }) => {
  await stubNewFeature(page);
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);
  await page.getByTestId(ids.newFeatureTitle).fill('Express');

  await page.getByTestId(ids.newFeatureSlug).fill('my-own');
  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');

  await expect(page.getByTestId(ids.newFeatureSlug)).toHaveValue('my-own');
});

test('NewFeatureDialog_WhenCreated_ShouldSendTheFieldsAndStartState', async ({ mount, page }) => {
  const server = await stubNewFeature(page);
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);
  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');
  await page.getByTestId(ids.newFeatureEpic).selectOption({ label: 'Checkout conversion' });
  await page.getByTestId(ids.newFeatureGoal).fill('Pay in one tap.');
  await page
    .getByTestId(ids.newFeatureStart)
    .getByTestId(ids.newFeatureStartOption)
    .filter({ hasText: 'Active' })
    .click();
  await page.getByTestId(ids.newFeatureStatus).fill('Waiting on the provider.');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeEnabled();

  const created = page.waitForResponse('**/api/work-items');
  await page.getByTestId(ids.newFeatureCreate).click();
  await created;

  expect(server.creates).toHaveLength(1);
  expect(server.creates[0]).toMatchObject({
    type: 'feature',
    title: 'Express checkout',
    parentId: 'e1',
    slug: 'express-checkout',
    goal: 'Pay in one tap.',
    statusLine: 'Waiting on the provider.',
    changes: { state: { value: 's-active' } },
  });
});

test('NewFeatureDialog_WhenCreateSaysSlugTaken_ShouldShowItOnTheSlugField', async ({
  mount,
  page,
}) => {
  await stubNewFeature(page, {
    create: {
      status: 409,
      body: { error: 'That slug is taken.', code: 'slug_taken', field: 'slug' },
    },
  });
  await mount(<NewFeatureDialog project={PROJECT} onClose={() => undefined} />, hooks);
  await page.getByTestId(ids.newFeatureTitle).fill('Express checkout');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeEnabled();

  await page.getByTestId(ids.newFeatureCreate).click();

  await expect(page.getByTestId(ids.newFeatureSlugNote)).toHaveText('That slug is taken.');
  await expect(page.getByTestId(ids.newFeatureCreate)).toBeDisabled();
});
