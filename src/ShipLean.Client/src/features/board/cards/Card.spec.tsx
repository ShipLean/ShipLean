import { expect, test } from '@playwright/experimental-ct-react';
import type { CardDto } from '../../../api/views';
import { componentTestIds } from '../../../components/testIds';
import { boardTestIds as ids } from '../testIds';
import { Card } from './Card';
import { card, PROJECT, SETTINGS, TOM } from '../ct/fixtures';

const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const withBugs = {
  ...SETTINGS,
  annotations: { ...SETTINGS.annotations, tasks: false, bugs: true },
};
const feature: Partial<CardDto> & { key: string } = {
  type: { id: 'feature', name: 'Feature' },
  key: 'SHIP-103',
  title: 'Mobile sign-in',
  points: 20,
  childPoints: 26,
  sumWarn: true,
  assignee: TOM,
  parent: {
    id: 'e1',
    key: 'SHIP-90',
    type: { id: 'epic', name: 'Epic' },
    title: 'Onboarding',
  },
  rollups: [
    { type: 'story', open: 2, closed: 3 },
    { type: 'bug', open: 0, closed: 1 },
    { type: 'task', open: 4, closed: 4 },
  ],
};

test('Card_WhenFull_ShouldShowKeyPointsTitleParentTagsAndAssignee', async ({ mount, page }) => {
  const story = card({
    key: 'SHIP-158',
    title: 'Saved filters',
    points: 5,
    assignee: TOM,
    parent: {
      id: 'f1',
      key: 'SHIP-103',
      type: { id: 'feature', name: 'Feature' },
      title: 'Mobile',
    },
    tags: [
      { name: 'Payments', colour: 'info' },
      { name: 'misc', colour: null },
    ],
    rollups: [{ type: 'task', open: 2, closed: 2 }],
  });
  await mount(
    <Card
      card={story}
      projectKey={PROJECT.key}
      level="stories"
      compact={false}
      settings={SETTINGS}
      inDone={false}
    />,
    route,
  );

  await expect(page.getByTestId(ids.cardKey)).toHaveText('SHIP-158');
  await expect(page.getByTestId(ids.cardPoints)).toHaveText('5');
  await expect(page.getByTestId(ids.cardTitle)).toHaveText('Saved filters');
  await expect(page.getByTestId(ids.cardTitle)).toHaveAttribute('href', '/p/SHIP/items/SHIP-158');
  await expect(page.getByTestId(ids.cardParent)).toContainText('SHIP-103 Mobile');
  await expect(page.getByTestId(ids.cardTag)).toHaveText(['Payments', 'misc']);
  await expect(page.getByTestId(ids.cardTag).nth(0)).toHaveAttribute('data-colour', 'info');
  await expect(page.getByTestId(ids.cardRollup)).toHaveText('T2/4');
  await expect(page.getByTestId(componentTestIds.avatar)).toHaveAttribute('data-variant', 'person');
});

test('Card_WhenUnestimatedAndUnassigned_ShouldWarnQuestionMarkAndDashedAvatar', async ({
  mount,
  page,
}) => {
  await mount(
    <Card
      card={card({ key: 'SHIP-1', points: null })}
      projectKey="SHIP"
      level="stories"
      compact={false}
      settings={SETTINGS}
      inDone={false}
    />,
    route,
  );
  await expect(page.getByTestId(ids.cardPoints)).toHaveText('?');
  await expect(page.getByTestId(ids.cardPoints)).toHaveAttribute('data-unestimated', 'true');
  await expect(page.getByTestId(componentTestIds.avatar)).toHaveAttribute('data-variant', 'none');
});

test('Card_WhenCompact_ShouldDropParentTagsAndRollupsOnStories', async ({ mount, page }) => {
  const story = card({
    key: 'SHIP-158',
    assignee: TOM,
    parent: {
      id: 'f1',
      key: 'SHIP-103',
      type: { id: 'feature', name: 'Feature' },
      title: 'Mobile',
    },
    tags: [{ name: 'Payments', colour: 'info' }],
    rollups: [{ type: 'task', open: 2, closed: 2 }],
  });
  await mount(
    <Card
      card={story}
      projectKey="SHIP"
      level="stories"
      compact
      settings={SETTINGS}
      inDone={false}
    />,
    route,
  );
  await expect(page.getByTestId(ids.card)).toHaveAttribute('data-compact', 'true');
  await expect(page.getByTestId(ids.cardKey)).toBeVisible();
  await expect(page.getByTestId(ids.cardPoints)).toBeVisible();
  await expect(page.getByTestId(ids.cardAssignee)).toBeVisible();
  await expect(page.getByTestId(ids.cardParent)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardTag)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardRollup)).toHaveCount(0);
});

test('Card_WhenFeature_ShouldShowSumParentLineAndRollupsPerAnnotation', async ({ mount, page }) => {
  await mount(
    <Card
      card={card(feature)}
      projectKey="SHIP"
      level="features"
      compact={false}
      settings={withBugs}
      inDone={false}
    />,
    route,
  );
  await expect(page.getByTestId(ids.cardPoints)).toHaveText('20');
  await expect(page.getByTestId(ids.cardChildPoints)).toHaveText('Σ26');
  await expect(page.getByTestId(ids.cardChildPoints)).toHaveAttribute('data-warn', 'true');
  await expect(page.getByTestId(ids.cardParent)).toContainText('SHIP-90 Onboarding');
  // Tasks are off in the annotations, so the card's task roll-up is left out.
  const rollups = page.getByTestId(ids.cardRollup);
  await expect(rollups).toHaveText(['S3/5', 'B1/1']);
  await expect(rollups.nth(0)).toHaveAttribute('data-complete', 'false');
  await expect(rollups.nth(1)).toHaveAttribute('data-complete', 'true');
});

test('Card_WhenCompactFeature_ShouldKeepRollupsButDropParent', async ({ mount, page }) => {
  await mount(
    <Card
      card={card(feature)}
      projectKey="SHIP"
      level="features"
      compact
      settings={withBugs}
      inDone={false}
    />,
    route,
  );
  await expect(page.getByTestId(ids.cardRollup)).toHaveCount(2);
  await expect(page.getByTestId(ids.cardParent)).toHaveCount(0);
});

test('Card_WhenStyleRuleMatches_ShouldTintFromItsColourAndBoldTheTitle', async ({
  mount,
  page,
}) => {
  await mount(
    <Card
      card={card({ key: 'SHIP-9', styleRuleId: 'rule-blocked' })}
      projectKey="SHIP"
      level="stories"
      compact={false}
      settings={SETTINGS}
      inDone={false}
    />,
    route,
  );
  const element = page.getByTestId(ids.card);
  await expect(element).toHaveAttribute('data-rule', 'rule-blocked');
  await expect(element).toHaveCSS('box-shadow', /inset/);
  await expect(page.getByTestId(ids.cardTitle)).toHaveCSS('font-weight', '600');
});

test('Card_WhenFieldsHidden_ShouldHideKeyPointsAssigneeAndTags', async ({ mount, page }) => {
  const settings = {
    ...SETTINGS,
    cardFields: [
      {
        type: 'story' as const,
        showKey: false,
        showAssignee: false,
        showTags: false,
        showPoints: false,
        showEmptyFields: false,
        extraFields: [],
      },
    ],
  };
  await mount(
    <Card
      card={card({ key: 'SHIP-9', tags: [{ name: 'x', colour: null }] })}
      projectKey="SHIP"
      level="stories"
      compact={false}
      settings={settings}
      inDone={false}
    />,
    route,
  );
  await expect(page.getByTestId(ids.cardKey)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardPoints)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardAssignee)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardTag)).toHaveCount(0);
});

test('Card_WhenExtraFields_ShouldListThemWithDashForEmpty', async ({ mount, page }) => {
  const withFields = card({
    key: 'SHIP-9',
    extraFields: [
      { fieldId: 'priority', label: 'Priority', text: '1' },
      { fieldId: 'severity', label: 'Severity', text: null },
    ],
  });
  await mount(
    <Card
      card={withFields}
      projectKey="SHIP"
      level="stories"
      compact={false}
      settings={SETTINGS}
      inDone={false}
    />,
    route,
  );
  const extras = page.getByTestId(ids.cardExtra);
  await expect(extras).toHaveCount(2);
  await expect(extras.nth(0)).toContainText('Priority');
  await expect(extras.nth(1)).toContainText('—');
});

test('Card_WhenTagsHidden_ShouldCloseUpAndKeepTheAssigneeOnTheParentLine', async ({
  mount,
  page,
}) => {
  const story = card({
    key: 'SHIP-46',
    assignee: TOM,
    parent: {
      id: 'f1',
      key: 'SHIP-103',
      type: { id: 'feature', name: 'Feature' },
      title: 'Mobile',
    },
    tags: [{ name: 'Payments', colour: 'info' }],
  });
  const hidden = {
    ...SETTINGS,
    cardFields: [{ ...SETTINGS.cardFields[0], showTags: false }],
  };
  const props = {
    card: story,
    projectKey: 'SHIP',
    level: 'stories',
    compact: false,
    inDone: false,
  } as const;
  const component = await mount(<Card {...props} settings={SETTINGS} />, route);
  const withTags = (await page.getByTestId(ids.card).boundingBox())!.height;

  await component.update(<Card {...props} settings={hidden} />);
  await expect(page.getByTestId(ids.cardTag)).toHaveCount(0);
  const withoutTags = (await page.getByTestId(ids.card).boundingBox())!.height;
  expect(withoutTags).toBeLessThan(withTags - 10);
  const parent = (await page.getByTestId(ids.cardParent).boundingBox())!;
  const assignee = (await page.getByTestId(ids.cardAssignee).boundingBox())!;
  expect(Math.abs(assignee.y + assignee.height / 2 - (parent.y + parent.height / 2))).toBeLessThan(
    2,
  );
});

test('Card_WhenNothingIsLeftBelowTheTitle_ShouldLeaveNoEmptyLine', async ({ mount, page }) => {
  const settings = {
    ...SETTINGS,
    cardFields: [{ ...SETTINGS.cardFields[0], showAssignee: false, showTags: false }],
  };
  await mount(
    <Card
      card={card({ key: 'SHIP-46', tags: [{ name: 'x', colour: null }] })}
      projectKey="SHIP"
      level="stories"
      compact={false}
      settings={settings}
      inDone={false}
    />,
    route,
  );
  // The key line and the title, and nothing else taking a grid row (and its gap).
  const rows = await page
    .getByTestId(ids.card)
    .evaluate(
      (element) =>
        [...element.children].filter((child) => getComputedStyle(child).display !== 'none').length,
    );
  expect(rows).toBe(2);
});
