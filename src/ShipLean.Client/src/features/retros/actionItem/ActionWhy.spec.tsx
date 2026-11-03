import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroActionDetailDto, RetroFixType } from '../../../api/retros';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retroActionDetail, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ActionWhy } from './ActionWhy';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-189' } };
const [first, second] = retroActionDetail.points;
const themeOf = retroActionDetail.points[1].theme!;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

/** Answers every action GET with `detail`; returns the paths it was asked for. */
async function answerWith(page: Page, detail: RetroActionDetailDto) {
  const asked: string[] = [];
  await page.route(/\/api\/retros\/actions\/[^/]+$/, (r) => {
    if (r.request().method() !== 'GET') return r.fallback();
    asked.push(new URL(r.request().url()).pathname);
    return r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(detail),
    });
  });
  return asked;
}

/** Answers every action GET with a failure; returns the paths it was asked for. */
async function failWith(page: Page, status: number, error: string, code: string) {
  const asked: string[] = [];
  await page.route(/\/api\/retros\/actions\/[^/]+$/, (r) => {
    asked.push(new URL(r.request().url()).pathname);
    return r.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify({ error, code }),
    });
  });
  return asked;
}

const withFix = (fixType: RetroFixType | null, theme = themeOf): RetroActionDetailDto => ({
  ...retroActionDetail,
  action: { ...retroActionDetail.action, fixType },
  theme,
});

function mountWhy(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  itemType = 'action' as const,
) {
  return mount(
    <RetroActionDialogProvider projectId="p1">
      <ActionWhy projectId="p1" projectKey="SHIP" workItemId="w189" itemType={itemType} />
      <ToastHost />
    </RetroActionDialogProvider>,
    route,
  );
}

test('quotes points with links back', async ({ mount, page }) => {
  await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await mountWhy(mount);
  const notes = page.getByTestId(ids.retroActionWhyPoint);
  await expect(notes).toHaveCount(2);
  await expect(notes.first()).toContainText(first.text);
  await expect(notes.first().getByTestId(ids.retroActionWhyLink)).toHaveAttribute(
    'href',
    `/p/SHIP/retros/items/SHIP-101?point=${first.id}`,
  );
  await expect(page.getByTestId(ids.retroActionWhy)).toContainText('2 points');
  await expect(page.getByTestId(ids.retroActionWhyFix)).toHaveValue('guide');
  await expect(page.getByTestId(ids.retroActionWhyTheme)).toHaveValue(themeOf.id);
});

test('links a sprint-level point to the sprint Retro tab with point=', async ({ mount, page }) => {
  await stubRetros(page);
  const sprintLevel = { ...second, workItem: null };
  await answerWith(page, { ...withFix('guide'), points: [sprintLevel] });
  await mountWhy(mount);
  await expect(page.getByTestId(ids.retroActionWhyLink)).toHaveAttribute(
    'href',
    `/p/SHIP/sprints/${second.sprint!.id}/retro?point=${second.id}`,
  );
});

test('changing theme calls PUT with the current fix type', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await mountWhy(mount);
  const other = first.theme!;
  await page.getByTestId(ids.retroActionWhyTheme).selectOption(other.id);
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0]).toMatchObject({
    method: 'PUT',
    path: '/api/retros/actions/w189',
    body: { fixType: 'guide', themeId: other.id, themeName: null },
  });
});

test('changing type of fix calls PUT with the current theme', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await mountWhy(mount);
  await page.getByTestId(ids.retroActionWhyFix).selectOption('test');
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toEqual({ fixType: 'test', themeId: themeOf.id, themeName: null });
});

test('a theme change on a legacy action keeps its fix null', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await answerWith(page, withFix(null));
  await mountWhy(mount);
  await expect(page.getByTestId(ids.retroActionWhyFix)).toHaveValue('');
  await page.getByTestId(ids.retroActionWhyTheme).selectOption(first.theme!.id);
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toEqual({
    fixType: null,
    themeId: first.theme!.id,
    themeName: null,
  });
});

test('the fix select has no empty option once a fix is stored', async ({ mount, page }) => {
  await stubRetros(page);
  await answerWith(page, withFix('skill'));
  await mountWhy(mount);
  await expect(page.getByTestId(ids.retroActionWhyFix).locator('option[value=""]')).toHaveCount(0);
});

test('+ Add points opens the dialog on this item', async ({ mount, page }) => {
  await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await mountWhy(mount);
  await page.getByTestId(ids.retroActionWhyAdd).click();
  await expect(page.getByTestId(shared.retroActionDialog)).toBeVisible();
  await expect(page.getByTestId(shared.retroActionModeExisting)).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});

test('failed PUT shows a toast', async ({ mount, page }) => {
  await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await page.route(/\/api\/retros\/actions\/[^/]+$/, (r) =>
    r.request().method() === 'PUT'
      ? r.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Fix type is required.', code: 'validation' }),
        })
      : r.fallback(),
  );
  await mountWhy(mount);
  await page.getByTestId(ids.retroActionWhyFix).selectOption('test');
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Fix type is required.');
});

test('not shown on a story (and shown on an action item)', async ({ mount, page }) => {
  await stubRetros(page);
  const asked = await answerWith(page, withFix('guide'));
  await mount(
    <RetroActionDialogProvider projectId="p1">
      <ActionWhy projectId="p1" projectKey="SHIP" workItemId="w189" itemType="action" />
      <ActionWhy projectId="p1" projectKey="SHIP" workItemId="w190" itemType="story" />
    </RetroActionDialogProvider>,
    route,
  );
  await expect(page.getByTestId(ids.retroActionWhy)).toHaveCount(1);
  await expect(page.getByTestId(ids.retroActionWhy)).toContainText('Why this exists');
  expect(asked).toEqual(['/api/retros/actions/w189']);
});

test('an action item with no retro_action row shows nothing, not an alert', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const asked = await failWith(page, 404, 'No action item with that id was found.', 'not_found');
  await mountWhy(mount);
  await expect.poll(() => asked.length).toBe(1);
  await expect(page.getByTestId(ids.retroActionWhy)).toHaveCount(0);
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('another failed read still shows as an alert', async ({ mount, page }) => {
  await stubRetros(page);
  await failWith(page, 500, 'The server failed.', 'internal_error');
  await mountWhy(mount);
  await expect(page.getByRole('alert')).toContainText('The server failed.');
});

test('two quick changes: the second PUT carries the first change', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  const other = first.theme!;
  // The server keeps what was last PUT, as the real one does.
  await page.route(/\/api\/retros\/actions\/[^/]+$/, (r) => {
    if (r.request().method() !== 'GET') return r.fallback();
    const put = stub.writes().at(-1)?.body as
      { fixType: RetroFixType | null; themeId: string | null } | undefined;
    const themes = [themeOf, other];
    const detail = put
      ? {
          ...withFix(put.fixType),
          theme: themes.find((t) => t.id === put.themeId) ?? null,
        }
      : withFix('guide');
    return r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(detail),
    });
  });
  await mountWhy(mount);
  await expect(page.getByTestId(ids.retroActionWhyFix)).toHaveValue('guide');
  await page.getByTestId(ids.retroActionWhyTheme).selectOption(other.id);
  await page.getByTestId(ids.retroActionWhyFix).selectOption('test');
  await expect.poll(() => stub.writes().length).toBe(2);
  expect(stub.writes()[1].body).toEqual({ fixType: 'test', themeId: other.id, themeName: null });
});
