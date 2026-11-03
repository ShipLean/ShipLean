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

async function answerWith(page: Page, detail: RetroActionDetailDto) {
  await page.route(/\/api\/retros\/actions\/[^/]+$/, (r) =>
    r.request().method() === 'GET'
      ? r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(detail) })
      : r.fallback(),
  );
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

test('not shown on a story', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await answerWith(page, withFix('guide'));
  await mount(
    <div>
      <RetroActionDialogProvider projectId="p1">
        <ActionWhy projectId="p1" projectKey="SHIP" workItemId="w189" itemType="story" />
      </RetroActionDialogProvider>
      <p>marker</p>
    </div>,
    route,
  );
  await expect(page.getByText('marker')).toBeVisible();
  await expect(page.getByTestId(ids.retroActionWhy)).toHaveCount(0);
  expect(stub.reads('/retros/actions/w189')).toHaveLength(0);
});
