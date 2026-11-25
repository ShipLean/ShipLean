import { expect, test } from '@playwright/experimental-ct-react';
import { PersonAvatar } from '../parts/PersonAvatar';
import { TypeBadge } from '../parts/TypeBadge';
import { timelineTestIds as ids } from '../testIds';

test('TypeBadge_ShouldShowSTBWithDistinctColours', async ({ mount, page }) => {
  await mount(
    <div>
      <TypeBadge type="story" />
      <TypeBadge type="task" />
      <TypeBadge type="bug" />
    </div>,
  );
  const badges = page.getByTestId(ids.typeBadge);
  await expect(badges).toHaveText(['S', 'T', 'B']);
  await expect(page.getByRole('img', { name: 'Story' })).toHaveAttribute('data-type', 'story');
  await expect(page.getByRole('img', { name: 'Task' })).toHaveAttribute('data-type', 'task');
  await expect(page.getByRole('img', { name: 'Bug' })).toHaveAttribute('data-type', 'bug');
  const colours = await badges.evaluateAll((els) =>
    els.map((el) => getComputedStyle(el).backgroundColor),
  );
  expect(colours).toHaveLength(3);
  expect(new Set(colours).size).toBe(3);
  for (const colour of colours) expect(colour).not.toBe('rgba(0, 0, 0, 0)');
});

test('PersonAvatar_WhenPerson_ShouldShowInitialsWithNameAsLabel', async ({ mount, page }) => {
  await mount(<PersonAvatar person={{ userId: 'u-1', name: 'Ana Lima', agent: false }} />);
  const avatar = page.getByRole('img', { name: 'Ana Lima' });
  await expect(avatar).toHaveText('AL');
  await expect(avatar).toHaveAttribute('data-variant', 'person');
  const background = await avatar.evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(background).not.toBe('rgba(0, 0, 0, 0)');
});

test('PersonAvatar_WhenSamePerson_ShouldKeepTheSameColour', async ({ mount, page }) => {
  await mount(
    <div>
      <PersonAvatar person={{ userId: 'u-1', name: 'Ana Lima', agent: false }} />
      <PersonAvatar person={{ userId: 'u-1', name: 'Ana Lima', agent: false }} size="sm" />
    </div>,
  );
  const colours = await page
    .getByTestId(ids.personAvatar)
    .evaluateAll((els) => els.map((el) => getComputedStyle(el).backgroundColor));
  expect(colours).toHaveLength(2);
  expect(colours[0]).toBe(colours[1]);
});

test('PersonAvatar_WhenAgent_ShouldSayAgentAndShowAI', async ({ mount, page }) => {
  await mount(<PersonAvatar person={{ userId: null, name: 'Claude Code', agent: true }} />);
  const avatar = page.getByRole('img', { name: 'Agent: Claude Code' });
  await expect(avatar).toHaveText('AI');
  await expect(avatar).toHaveAttribute('data-variant', 'agent');
});

test('PersonAvatar_WhenNull_ShouldSayUnassignedWithADashedBorder', async ({ mount, page }) => {
  await mount(<PersonAvatar person={null} />);
  const avatar = page.getByRole('img', { name: 'Unassigned' });
  await expect(avatar).toBeVisible();
  await expect(avatar).toHaveText('');
  await expect(avatar).toHaveAttribute('data-variant', 'none');
  await expect(avatar).toHaveCSS('border-top-style', 'dashed');
});
