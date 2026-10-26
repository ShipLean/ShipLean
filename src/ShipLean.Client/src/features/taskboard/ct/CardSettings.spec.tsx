import { expect, test } from '@playwright/experimental-ct-react';
import type { CardFieldSettingJson } from '../../../api/persisted';
import type { CardDto, TaskboardViewDto } from '../../../api/views';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { componentTestIds } from '../../../components/testIds';
import { boardTestIds } from '../../board/testIds';
import { taskboardTestIds as ids } from '../testIds';
import { PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

const POSTIT_PAPER = 'rgb(255, 249, 211)';
const POSTIT_MUTED = 'rgb(110, 93, 34)';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />;

const setting = (
  type: CardFieldSettingJson['type'],
  extraFields: string[],
): CardFieldSettingJson => ({
  type,
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: true,
  showEmptyFields: false,
  extraFields,
});

const withTasks = (
  view: TaskboardViewDto,
  change: (card: CardDto) => CardDto,
): TaskboardViewDto => ({
  ...view,
  rows: view.rows.map((row) => ({
    ...row,
    cells: row.cells.map((cell) => ({ ...cell, tasks: cell.tasks.map(change) })),
  })),
});

const people = { saved: { groupBy: 'people' } } as const;

test('Taskboard_WhenGroupedByPeopleAndParentExtraOn_ShouldShowTheParentLine', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [setting('task', ['parent'])] }, people);
  await mount(slot, route);

  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  await expect(page.getByTestId(ids.cardParent).first()).toBeVisible();
});

test('Taskboard_WhenGroupedByPeopleAndParentExtraOff_ShouldNotShowTheParentLine', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [setting('task', [])] }, people);
  await mount(slot, route);

  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  await expect(page.getByTestId(ids.cardParent)).toHaveCount(0);
});

test('Taskboard_WhenGroupedByStoriesAndParentExtraOn_ShouldNotShowTheParentLine', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [setting('task', ['parent'])] });
  await mount(slot, route);

  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  await expect(page.getByTestId(ids.cardParent)).toHaveCount(0);
});

test('Taskboard_WhenBoardHasNoTaskSetting_ShouldShowTheParentLineWhenGroupedByPeople', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [] }, people);
  await mount(slot, route);

  await expect(page.getByTestId(ids.cardParent).first()).toBeVisible();
});

test('Taskboard_WhenStoryExtraFieldsSet_ShouldDrawThemInTheRowHeader', async ({ mount, page }) => {
  const view: TaskboardViewDto = {
    ...VIEW,
    cardFields: [setting('story', ['severity'])],
    rows: VIEW.rows.map((row) =>
      row.parent
        ? {
            ...row,
            parent: {
              ...row.parent,
              extraFields: [{ fieldId: 'severity', label: 'Severity', text: 'High' }],
            },
          }
        : row,
    ),
  };
  await stubTaskboard(page, view);
  await mount(slot, route);

  const extra = page.getByTestId(ids.rowHeader).first().getByTestId(boardTestIds.cardExtra);
  await expect(extra).toContainText('Severity');
  await expect(extra).toContainText('High');
});

test('Taskboard_WhenStateExtraOn_ShouldShowTheStateSelectInTheRowHeader', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [setting('story', ['state'])] });
  await mount(slot, route);

  await expect(page.getByTestId(ids.rowState)).toHaveCount(1);
});

test('Taskboard_WhenStateExtraOff_ShouldHideTheStateSelectInTheRowHeader', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, { ...VIEW, cardFields: [setting('story', [])] });
  await mount(slot, route);

  await expect(page.getByTestId(ids.rowHeader).first()).toBeVisible();
  await expect(page.getByTestId(ids.rowState)).toHaveCount(0);
});

test('Taskboard_WhenTaskProgressOff_ShouldHideTheProgressInTheRowHeader', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, {
    ...VIEW,
    annotations: { ...VIEW.annotations, tasks: false, stories: true },
  });
  await mount(slot, route);

  await expect(page.getByTestId(ids.rowHeader).first()).toBeVisible();
  await expect(page.getByTestId(ids.rowProgress)).toHaveCount(0);
});

test('Taskboard_WhenCardsCarryExtraFields_ShouldDrawThemAsRows', async ({ mount, page }) => {
  const view = withTasks({ ...VIEW, cardFields: [setting('task', ['priority'])] }, (card) => ({
    ...card,
    extraFields: [{ fieldId: 'priority', label: 'Priority', text: '2' }],
  }));
  await stubTaskboard(page, view);
  await mount(slot, route);

  const extra = page.getByTestId(ids.card).first().getByTestId(boardTestIds.cardExtra);
  await expect(extra).toContainText('Priority');
  await expect(extra).toContainText('2');
});

test('Taskboard_WhenRuleColoursACard_ShouldBorderItAndColourItsTags', async ({ mount, page }) => {
  const view = withTasks(
    {
      ...VIEW,
      styleRules: [
        {
          id: 'r1',
          name: 'Blocked',
          enabled: true,
          colour: 'danger',
          bold: true,
          italic: false,
          underline: false,
          criteria: { match: 'all', clauses: [] },
        },
      ],
    },
    (card) => ({
      ...card,
      styleRuleId: card.key === 'SHIP-171' ? 'r1' : null,
      tags: [{ name: 'Blocked', colour: 'warn' }],
    }),
  );
  await stubTaskboard(page, view);
  await mount(slot, route);

  const styled = page.locator(`[data-testid="${ids.card}"][data-style-rule="r1"]`);
  await expect(styled).toHaveCount(1);
  await expect(styled).toHaveCSS('font-weight', '700');
  // A ruled post-it is tinted past the plain paper and keeps the 3px left bar (an inset shadow).
  await expect(styled).not.toHaveCSS('background-color', POSTIT_PAPER);
  await expect(styled).toHaveCSS('box-shadow', /3px 0px 0px 0px inset/);
  await expect(
    page.getByTestId(ids.cardTag).first().getByTestId(componentTestIds.chip),
  ).toHaveAttribute('data-tone', 'warn');
});

for (const theme of ['light', 'dark'] as const) {
  test(`Taskboard_When${theme}Theme_ShouldDrawTaskCardsAsPostits`, async ({ mount, page }) => {
    await stubTaskboard(page, VIEW);
    await mount(slot, route);
    await page.evaluate(
      (value) => document.documentElement.setAttribute('data-theme', value),
      theme,
    );

    const card = page.getByTestId(ids.card).first();
    await expect(card).toHaveCSS('background-color', POSTIT_PAPER);
    await expect(card).toHaveCSS('border-radius', '2px');
    await expect(card.getByTestId(ids.cardKey)).toHaveCSS('color', POSTIT_MUTED);
  });
}
