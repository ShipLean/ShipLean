import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { PromptKey } from '@shiplean/core/api/prompts';
import { adminPage, adminProject } from './ct/promptsData';
import { HeaderSearch } from './header/HeaderSearch';
import { WaysOfWorkingHeader } from './header/WaysOfWorkingHeader';
import { promptsTestIds as ids } from './testIds';

const SEARCH_PICKED_URL = '**/api/projects/*/usage/prompts-search-picked';

const hit = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.searchResult}"][data-prompt="${key}"]`);

/** Records the search-picked posts and answers each with 204. */
async function recordPicks(page: Page): Promise<{ url: string; body: unknown }[]> {
  const posts: { url: string; body: unknown }[] = [];
  await page.route(SEARCH_PICKED_URL, async (route) => {
    posts.push({ url: route.request().url(), body: route.request().postDataJSON() });
    await route.fulfill({ status: 204 });
  });
  return posts;
}

test('HeaderSearch_WhenTyped_ShouldListHitsWithStageFileStatusAndLine', async ({ mount, page }) => {
  await mount(<HeaderSearch projectId="p-ship" page={adminPage} onPick={() => undefined} />);

  await page.getByTestId(ids.searchInput).fill('second line');

  await expect(page.getByTestId(ids.searchResults)).toBeVisible();
  // Every prompt but guide, whose custom text in use has no second line.
  await expect(page.getByTestId(ids.searchResult)).toHaveCount(11);
  const brief = hit(page, 'brief');
  await expect(brief.getByTestId(ids.searchResultName)).toHaveText('Task brief');
  await expect(brief.getByTestId(ids.searchResultStage)).toHaveText('4 · Build · brief.md');
  await expect(brief.getByTestId(ids.searchResultStatus)).toHaveText('Custom v2');
  await expect(hit(page, 'issueProposal').getByTestId(ids.searchResultStatus)).toHaveText(
    'Using built-in',
  );
  await expect(hit(page, 'work').getByTestId(ids.searchResultStatus)).toHaveText('Built-in');
  const work = hit(page, 'work');
  await expect(work.getByTestId(ids.searchResultLine)).toHaveText('Second line.');
  await expect(work.getByTestId(ids.searchMatch)).toHaveText('Second line');
  await expect(page.getByTestId(ids.searchFoot)).toHaveText(
    '11 of 12 prompts · Enter opens the first',
  );
  await expect(brief.getByTestId(ids.searchResultLine)).toHaveText(
    'Second line, reworded by the team.',
  );

  await page.getByTestId(ids.searchInput).fill('our own');
  await expect(page.getByTestId(ids.searchResult)).toHaveCount(1);
  await expect(hit(page, 'guide').getByTestId(ids.searchResultStatus)).toHaveText(
    'Built-in updated',
  );
  await expect(hit(page, 'guide').getByTestId(ids.searchResultLine)).toHaveText('Our own line.');
});

test('HeaderSearch_WhenEnter_ShouldOpenTheFirstHitAndPostSearchPicked', async ({ mount, page }) => {
  const posts = await recordPicks(page);
  const picked: PromptKey[] = [];
  await mount(
    <HeaderSearch projectId="p-ship" page={adminPage} onPick={(key) => picked.push(key)} />,
  );

  await page.getByTestId(ids.searchInput).fill('reviewer');
  await expect(page.getByTestId(ids.searchResult)).toHaveCount(2);
  await expect(page.getByTestId(ids.searchResult).first()).toHaveAttribute(
    'data-prompt',
    'planReviewer',
  );
  await page.getByTestId(ids.searchInput).press('Enter');

  await expect.poll(() => picked).toEqual(['planReviewer']);
  await expect
    .poll(() => posts)
    .toEqual([
      {
        url: expect.stringContaining('/api/projects/p-ship/usage/prompts-search-picked'),
        body: { prompt: 'planReviewer' },
      },
    ]);
  await expect(page.getByTestId(ids.searchInput)).toHaveValue('');
  await expect(page.getByTestId(ids.searchResults)).toHaveCount(0);
});

test('HeaderSearch_WhenArrowDownAndEnter_ShouldOpenTheSecondHit', async ({ mount, page }) => {
  const posts = await recordPicks(page);
  const picked: PromptKey[] = [];
  await mount(
    <HeaderSearch projectId="p-ship" page={adminPage} onPick={(key) => picked.push(key)} />,
  );

  await page.getByTestId(ids.searchInput).fill('reviewer');
  await expect(hit(page, 'planReviewer')).toHaveAttribute('aria-selected', 'true');
  await page.getByTestId(ids.searchInput).press('ArrowDown');
  await expect(hit(page, 'codeReviewer')).toHaveAttribute('aria-selected', 'true');
  await expect(hit(page, 'planReviewer')).toHaveAttribute('aria-selected', 'false');
  await page.getByTestId(ids.searchInput).press('Enter');

  await expect.poll(() => picked).toEqual(['codeReviewer']);
  await expect.poll(() => posts.map((p) => p.body)).toEqual([{ prompt: 'codeReviewer' }]);
});

test('HeaderSearch_WhenResultClicked_ShouldPickItAndPostSearchPicked', async ({ mount, page }) => {
  const posts = await recordPicks(page);
  const picked: PromptKey[] = [];
  await mount(
    <HeaderSearch projectId="p-ship" page={adminPage} onPick={(key) => picked.push(key)} />,
  );

  await page.getByTestId(ids.searchInput).fill('reviewer');
  await hit(page, 'codeReviewer').click();

  await expect.poll(() => picked).toEqual(['codeReviewer']);
  await expect.poll(() => posts.map((p) => p.body)).toEqual([{ prompt: 'codeReviewer' }]);
});

test('HeaderSearch_WhenEscape_ShouldCloseTheMenu', async ({ mount, page }) => {
  const picked: PromptKey[] = [];
  await mount(
    <HeaderSearch projectId="p-ship" page={adminPage} onPick={(key) => picked.push(key)} />,
  );

  await page.getByTestId(ids.searchInput).fill('reviewer');
  await expect(page.getByTestId(ids.searchResults)).toBeVisible();
  await page.getByTestId(ids.searchInput).press('Escape');

  await expect(page.getByTestId(ids.searchResults)).toHaveCount(0);
  await expect(page.getByTestId(ids.searchInput)).toHaveValue('reviewer');
  await page.getByTestId(ids.searchInput).press('Enter');
  expect(picked).toEqual([]);
});

test('HeaderSearch_WhenNoMatch_ShouldSayNoPromptMatches', async ({ mount, page }) => {
  await mount(<HeaderSearch projectId="p-ship" page={adminPage} onPick={() => undefined} />);

  await page.getByTestId(ids.searchInput).fill('zzz-no-such-prompt');

  await expect(page.getByTestId(ids.searchResults)).toBeVisible();
  await expect(page.getByTestId(ids.searchEmpty)).toHaveText(
    'No prompt matches. Placeholders are searchable too, for example task_key.',
  );
  await expect(page.getByTestId(ids.searchResult)).toHaveCount(0);
});

test('WaysOfWorkingHeader_ShouldShowTitleSubtitleAndCustomPill', async ({ mount, page }) => {
  await mount(
    <WaysOfWorkingHeader
      project={{ ...adminProject, name: 'Storefront' }}
      customCount={3}
      unsaved={false}
      search={<HeaderSearch projectId="p-ship" page={adminPage} onPick={() => undefined} />}
    />,
  );

  const header = page.getByTestId(ids.header);
  await expect(header.getByRole('heading', { level: 1 })).toHaveText('Ways of working');
  await expect(header).toContainText(
    'The prompts agents in Storefront get, in the order they meet them',
  );
  await expect(page.getByTestId(ids.customCountPill)).toHaveText('3 custom');
  await expect(page.getByTestId(ids.unsavedPill)).toHaveCount(0);
  await expect(page.getByTestId(ids.searchInput)).toBeVisible();
});

test('WaysOfWorkingHeader_WhenUnsaved_ShouldShowTheAmberPillBeforeSearch', async ({
  mount,
  page,
}) => {
  await mount(
    <WaysOfWorkingHeader
      project={adminProject}
      customCount={2}
      unsaved
      search={<HeaderSearch projectId="p-ship" page={adminPage} onPick={() => undefined} />}
    />,
  );

  const pill = page.getByTestId(ids.unsavedPill);
  await expect(pill).toHaveText('● Unsaved custom prompt');
  const pillBox = await pill.boundingBox();
  const searchBox = await page.getByTestId(ids.searchInput).boundingBox();
  expect(pillBox).not.toBeNull();
  expect(searchBox).not.toBeNull();
  // Same row, pill first.
  expect(pillBox!.x + pillBox!.width).toBeLessThanOrEqual(searchBox!.x);
  expect(
    Math.abs(pillBox!.y + pillBox!.height / 2 - (searchBox!.y + searchBox!.height / 2)),
  ).toBeLessThan(4);
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('WaysOfWorkingHeader_WhenOnAPhone_ShouldPutSearchFullWidth', async ({ mount, page }) => {
    await mount(
      <WaysOfWorkingHeader
        project={{ ...adminProject, name: 'Storefront' }}
        customCount={3}
        unsaved={false}
        search={<HeaderSearch projectId="p-ship" page={adminPage} onPick={() => undefined} />}
      />,
    );

    const header = await page.getByTestId(ids.header).boundingBox();
    const input = await page.getByTestId(ids.searchInput).boundingBox();
    const pill = await page.getByTestId(ids.customCountPill).boundingBox();
    expect(header).not.toBeNull();
    expect(input).not.toBeNull();
    expect(pill).not.toBeNull();
    // The search runs the header's full width, on its own line under the pill.
    expect(input!.width).toBeGreaterThanOrEqual(header!.width - 1);
    expect(input!.y).toBeGreaterThanOrEqual(pill!.y + pill!.height);

    await page.getByTestId(ids.searchInput).fill('reviewer');
    const menu = await page.getByTestId(ids.searchResults).boundingBox();
    expect(menu).not.toBeNull();
    expect(Math.abs(menu!.width - input!.width)).toBeLessThan(1);
  });
});
