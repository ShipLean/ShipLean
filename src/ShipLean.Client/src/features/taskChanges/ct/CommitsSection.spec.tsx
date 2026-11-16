import { expect, test } from '@playwright/experimental-ct-react';
import type { TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { ChangesSlot } from '../../../slots/ChangesSlot';
import { CommitsSection } from '../CommitsSection';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES, SECTION, TASK } from './fixtures';
import { stubChanges } from './stubChanges';

const route = { hooksConfig: { route: `/p/SHIP/items/${TASK.key}` } };
const EMPTY: TaskCommitsSectionDto = {
  ...SECTION,
  commits: [],
  totals: { commits: 0, files: 0, additions: 0, deletions: 0 },
  filePaths: [],
  openComments: 0,
  headCommitId: null,
};
const section = (props: Partial<Parameters<typeof CommitsSection>[0]> = {}) => (
  <CommitsSection
    projectKey="SHIP"
    itemKey={TASK.key}
    section={SECTION}
    viewedCount={0}
    onAddCommit={async () => {}}
    {...props}
  />
);

test('CommitsSection_WhenCommitsExist_ShouldShowEachRowAndTheTotals', async ({ mount, page }) => {
  await mount(section(), route);
  const rows = page.getByTestId(ids.sectionRow);
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText('a81d4e0');
  await expect(rows.first()).toContainText('Add the saved-card picker');
  await expect(rows.first()).toContainText('acme/storefront');
  await expect(rows.first()).toContainText('Claude Code');
  await expect(rows.first()).toContainText('Today');
  await expect(rows.first()).toContainText('+18');
  await expect(rows.first()).toContainText('−2');
  await expect(page.getByTestId(ids.sectionTotals)).toHaveText('3 commits · 3 files · +51 −3');
  await expect(page.getByTestId(ids.sectionOpenComments)).toHaveText('1 open comments');
});

test('CommitsSection_WhenARowHasAdditionsAndDeletions_ShouldDrawFiveBarsInProportion', async ({
  mount,
  page,
}) => {
  const base = SECTION.commits[0]!;
  const commits = [{ ...base, additions: 3, deletions: 2 }];
  await mount(section({ section: { ...SECTION, commits } }), route);
  const kinds = await page
    .getByTestId(ids.sectionBars)
    .first()
    .locator('i')
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-kind')));
  expect(kinds).toEqual(['add', 'add', 'add', 'del', 'del']);
});

test('CommitsSection_WhenACommitWasSentByAnAgent_ShouldShowTheSentByFooter', async ({
  mount,
  page,
}) => {
  const base = SECTION.commits[0]!;
  const commits = [{ ...base, addedBy: 'Claude Code', addedByAgent: true }];
  await mount(section({ section: { ...SECTION, commits } }), route);
  await expect(page.getByTestId(ids.sectionSentBy)).toHaveText(
    'Sent by Claude Code through report_task',
  );
});

test('CommitsSection_WhenNoCommitIsFromAnAgent_ShouldHideTheSentByFooter', async ({
  mount,
  page,
}) => {
  const commits = SECTION.commits.map((c) => ({ ...c, addedByAgent: false }));
  await mount(section({ section: { ...SECTION, commits } }), route);
  await expect(page.getByTestId(ids.sectionRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.sectionSentBy)).toHaveCount(0);
});

test('CommitsSection_WhenSomeFilesViewed_ShouldFillTheTrackToTheShare', async ({ mount, page }) => {
  await mount(section({ viewedCount: 1 }), route);
  const fill = page.getByTestId(ids.sectionViewedFill);
  const widths = await fill.evaluate((el) => [
    el.getBoundingClientRect().width,
    el.parentElement!.getBoundingClientRect().width,
  ]);
  expect(widths[0] / widths[1]).toBeCloseTo(1 / 3, 1);
});

test('ChangesSlot_WhenSomeFilesViewedAtTheHead_ShouldCountOnlyThose', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await stubChanges(page, TASK, CHANGES, SECTION);
  const marks = {
    // Confirmed at the current head: counts.
    'acme/storefront\u0000src/checkout/Checkout.tsx': {
      patchId: 'a',
      headCommitId: SECTION.headCommitId,
    },
    // Confirmed at an older head: does not.
    'acme/storefront\u0000src/checkout/SavedCards.tsx': { patchId: 'b', headCommitId: 'old-head' },
  };
  await page.evaluate(([key, value]) => localStorage.setItem(key, value), [
    `shiplean.taskChanges.viewed.${TASK.id}`,
    JSON.stringify(marks),
  ] as const);
  await mount(<ChangesSlot project={PROJECT} itemKey={TASK.key} />, route);
  await expect(page.getByTestId(ids.sectionViewed)).toHaveText('1 / 3 files viewed');
});

test('CommitsSection_WhenAddingAShaWithARepo_ShouldCallBackWithBoth', async ({ mount, page }) => {
  const calls: [string | null, string][] = [];
  await mount(
    section({
      section: EMPTY,
      onAddCommit: async (repo, sha) => {
        calls.push([repo, sha]);
      },
    }),
    route,
  );
  await page.getByTestId(ids.sectionAddOpen).click();
  await page.getByTestId(ids.sectionAddRepo).fill('acme/storefront');
  await page.getByTestId(ids.sectionAddInput).fill('3f9c2a17e4b0');
  await page.getByTestId(ids.sectionAddSubmit).click();
  await expect.poll(() => calls).toEqual([['acme/storefront', '3f9c2a17e4b0']]);
});

test('CommitsSection_WhenAddingACommitUrl_ShouldCallBackWithNullRepo', async ({ mount, page }) => {
  const calls: [string | null, string][] = [];
  await mount(
    section({
      section: EMPTY,
      onAddCommit: async (repo, sha) => {
        calls.push([repo, sha]);
      },
    }),
    route,
  );
  await page.getByTestId(ids.sectionAddOpen).click();
  await page.getByTestId(ids.sectionAddRepo).fill('ignored/repo');
  const url = 'https://github.com/acme/storefront/commit/3f9c2a17e4b0';
  await page.getByTestId(ids.sectionAddInput).fill(url);
  await page.getByTestId(ids.sectionAddSubmit).click();
  await expect.poll(() => calls).toEqual([[null, url]]);
});

const URL = 'https://github.com/acme/storefront/commit/3f9c2a1';
for (const [name, repo, input, body] of [
  [
    'ShouldPostRepoAndSha',
    'acme/storefront',
    '3f9c2a1',
    { repo: 'acme/storefront', sha: '3f9c2a1' },
  ],
  ['AndACommitUrl_ShouldPostItAsShaWithNullRepo', '', URL, { repo: null, sha: URL }],
] as const) {
  test(`ChangesSlot_WhenAddingACommit_${name}`, async ({ mount, page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await stubChanges(page, TASK, CHANGES, EMPTY);
    const bodies: unknown[] = [];
    await page.route(`**/api/work-items/${TASK.id}/commits`, async (r) => {
      if (r.request().method() !== 'POST') return r.fallback();
      bodies.push(r.request().postDataJSON());
      return r.fulfill({ json: SECTION });
    });
    await mount(<ChangesSlot project={PROJECT} itemKey={TASK.key} />, route);
    await page.getByTestId(ids.sectionAddOpen).click();
    if (repo !== '') await page.getByTestId(ids.sectionAddRepo).fill(repo);
    await page.getByTestId(ids.sectionAddInput).fill(input);
    await page.getByTestId(ids.sectionAddSubmit).click();
    await expect.poll(() => bodies).toEqual([body]);
  });
}

for (const [code, message] of [
  ['repo_unreadable', 'ShipLean cannot read acme/x.'],
  ['bad_sha', 'That is not a commit sha.'],
] as const) {
  test(`ChangesSlot_When${code}_ShouldShowTheErrorUnderTheForm`, async ({ mount, page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await stubChanges(page, TASK, CHANGES, EMPTY);
    await page.route(`**/api/work-items/${TASK.id}/commits`, async (r) => {
      if (r.request().method() !== 'POST') return r.fallback();
      return r.fulfill({ status: 422, json: { code, error: message } });
    });
    await mount(<ChangesSlot project={PROJECT} itemKey={TASK.key} />, route);
    await page.getByTestId(ids.sectionAddOpen).click();
    await page.getByTestId(ids.sectionAddInput).fill('abc1234');
    await page.getByTestId(ids.sectionAddSubmit).click();
    await expect(page.getByTestId(ids.sectionAddError)).toHaveText(message);
  });
}

test('CommitsSection_WhenReviewChangesIsClicked_ShouldLinkToTheChangesRoute', async ({
  mount,
  page,
}) => {
  await mount(section(), route);
  await expect(page.getByTestId(ids.sectionReview)).toHaveAttribute(
    'href',
    `/p/SHIP/items/${TASK.key}/changes`,
  );
});

test('CommitsSection_WhenThereAreNoCommits_ShouldShowTheEmptyTextAndNoReviewLink', async ({
  mount,
  page,
}) => {
  await mount(section({ section: EMPTY }), route);
  await expect(page.getByTestId(ids.sectionEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.sectionReview)).toHaveCount(0);
});
