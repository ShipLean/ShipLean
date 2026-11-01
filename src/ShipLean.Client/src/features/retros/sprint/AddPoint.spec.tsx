import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { sprintRetro, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { AddPoint } from './AddPoint';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;

test('AddPoint_WhenSprintPointSaved_ShouldPostSprintIdCategoryAndText', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<AddPoint project={project} sprint={sprint} retro={sprintRetro} />);
  await page.getByTestId(ids.retroAddPoint).click();
  await expect(page.getByTestId(ids.retroAddPointDialog)).toBeVisible();

  await page.getByTestId(ids.retroAddPointCategory).selectOption('improve');
  await page.getByTestId(ids.retroAddPointText).fill('Pair on the first slice');
  await expect(page.getByTestId(ids.retroAddPointRootCause)).toHaveCount(0);
  await page.getByTestId(ids.retroAddPointSave).click();

  await expect.poll(() => stub.writes().length).toBe(1);
  const write = stub.writes()[0];
  expect(write.method).toBe('POST');
  expect(write.path).toBe('/api/projects/p1/retros/points');
  expect(write.body).toEqual({
    retroId: null,
    workItemId: null,
    sprintId: 'sprint-24',
    category: 'improve',
    text: 'Pair on the first slice',
    rootCause: null,
    themeId: null,
    themeName: null,
  });
  await expect(page.getByTestId(ids.retroAddPointDialog)).toHaveCount(0);
});

test('AddPoint_WhenItemChosenAndWentBadly_ShouldPostWorkItemIdAndRootCause', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<AddPoint project={project} sprint={sprint} retro={sprintRetro} />);
  await page.getByTestId(ids.retroAddPoint).click();

  const options = page.getByTestId(ids.retroAddPointItem).locator('option');
  const values = await options.evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value));
  expect(new Set(values).size).toBe(values.length);
  expect(values[0]).toBe('');
  const item = sprintRetro.coverage[0].item;
  expect(values).toContain(item.id);

  await page.getByTestId(ids.retroAddPointItem).selectOption(item.id);
  await page.getByTestId(ids.retroAddPointText).fill('The migration was slow');
  await page.getByTestId(ids.retroAddPointRootCause).fill('No index on the join');
  await page.getByTestId(ids.retroAddPointSave).click();

  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toMatchObject({
    workItemId: item.id,
    sprintId: null,
    category: 'wentBadly',
    text: 'The migration was slow',
    rootCause: 'No index on the join',
  });
});

test('AddPoint_WhenServerRejects_ShouldShowTheErrorInlineAndKeepTheDialog', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await page.route(
    (url) => url.pathname === '/api/projects/p1/retros/points',
    (route) =>
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Text is too long.', code: 'validation', field: 'text' }),
      }),
  );
  await mount(<AddPoint project={project} sprint={sprint} retro={sprintRetro} />);
  await page.getByTestId(ids.retroAddPoint).click();
  await page.getByTestId(ids.retroAddPointText).fill('x');
  await page.getByTestId(ids.retroAddPointSave).click();

  await expect(page.getByTestId(ids.retroAddPointDialog).getByRole('alert')).toHaveText(
    'Text is too long.',
  );
  await expect(page.getByTestId(ids.retroAddPointDialog)).toBeVisible();
});
