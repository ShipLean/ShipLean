import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import none from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import working from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-working.json' with { type: 'json' };
import youtubeFailed from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-failed.json' with { type: 'json' };
import type { SprintDemoDto } from '@shiplean/core/api/demos';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { componentTestIds } from '../../components/testIds';
import { DemoPanelHarness } from './ct/DemoPanelHarness';
import { demosTestIds as ids } from './testIds';

// The Demo tab's body on a phone (SHIP-889, L889a) in the CT phone project: one column, the player at 16:9, the action
// under the message at full width with its reason visible. The header and tabs are the sprint header's own tests.

const project = { id: 'p1', key: 'SHIP', role: 'member' } as ProjectDto;
const readyDemo = ready as unknown as SprintDemoDto;
const sprint = { id: readyDemo.sprintId, name: 'Sprint 24' } as SprintDto;
const view = <DemoPanelHarness project={project} sprint={sprint} />;
const fixture = readFileSync(
  fileURLToPath(
    new URL('../../../../ShipLean.TestSupport/DemoFixtures/demo-fixture.mp4', import.meta.url),
  ),
);

// The real 8-second file, answered with byte ranges so the browser can seek it.
async function stubDemo(page: Page, demo: SprintDemoDto) {
  await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: demo }));
  if (demo.video === null) return;
  await page.route(`**${demo.video.contentUrl}`, (route) => {
    const range = /bytes=(\d+)-(\d*)/.exec(route.request().headers()['range'] ?? '');
    if (range === null) {
      return route.fulfill({
        body: fixture,
        headers: { 'content-type': 'video/mp4', 'accept-ranges': 'bytes' },
      });
    }
    const from = Number(range[1]);
    const to = range[2] === '' ? fixture.length - 1 : Number(range[2]);
    return route.fulfill({
      status: 206,
      body: fixture.subarray(from, to + 1),
      headers: {
        'content-type': 'video/mp4',
        'accept-ranges': 'bytes',
        'content-range': `bytes ${from}-${to}/${fixture.length}`,
      },
    });
  });
}

const blocked: SprintDemoDto = {
  ...(none as unknown as SprintDemoDto),
  status: 'blocked',
  statusMessage: "The automatic demo didn't start: Storefront has no agent connection.",
  actions: {
    canCreate: false,
    createBlockedMessage: 'Connect an agent to this project, then create the demo here.',
    createWarning: null,
    canRetryYouTube: false,
  },
};

test('SprintDemoPanel_WhenReadyOnAPhone_ShouldStackPlayerChaptersFeaturesAndScript', async ({
  mount,
  page,
}) => {
  await stubDemo(page, readyDemo);
  await mount(view);
  const boxes = [ids.demoPlayer, ids.demoChapters, ids.demoFeatures, ids.demoScript].map((id) =>
    page.getByTestId(id),
  );
  for (const box of boxes) await expect(box).toBeVisible();

  const [player, chapters, features, script] = await Promise.all(
    boxes.map(async (box) => (await box.boundingBox())!),
  );
  expect(chapters.y).toBeGreaterThanOrEqual(player.y + player.height);
  expect(features.y).toBeGreaterThanOrEqual(chapters.y + chapters.height);
  expect(script.y).toBeGreaterThanOrEqual(features.y + features.height);
  // One column: every block starts at the same left edge, inside the 12px gutter, and the page never scrolls sideways.
  for (const box of [chapters, features, script]) expect(box.x).toBeLessThan(player.x + 16);
  expect(player.x).toBeGreaterThanOrEqual(12);
  expect(chapters.width).toBeGreaterThan(390 - 40);
  const page_ = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(page_.scroll).toBe(page_.client);
});

test('DemoPlayer_WhenOnAPhone_ShouldFillTheWidthAt16By9', async ({ mount, page }) => {
  await stubDemo(page, readyDemo);
  await mount(view);
  const video = page.getByTestId(ids.demoVideo);
  await expect(video).toBeVisible();

  const frame = (await video.evaluate((el) => {
    const rect = el.parentElement!.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
  }))!;
  const panel = (await page.getByTestId(ids.demoPanel).boundingBox())!;
  expect(frame.width).toBeGreaterThan(panel.width - 24.5);
  expect(frame.height / frame.width).toBeCloseTo(9 / 16, 2);
});

test('DemoChapters_WhenChapterTappedOnAPhone_ShouldSeek', async ({ mount, page }) => {
  await stubDemo(page, readyDemo);
  await mount(view);
  const video = page.getByTestId(ids.demoVideo);
  await expect(video).toBeVisible();
  await video.evaluate((element: HTMLVideoElement) => {
    element.addEventListener('seeked', () => {
      element.dataset.seekedAt = String(element.currentTime);
    });
  });
  const second = page.getByTestId(ids.demoChapter).nth(1);
  expect((await second.boundingBox())!.height).toBeGreaterThanOrEqual(32);

  await second.tap();

  await expect(video).toHaveAttribute('data-seeked-at', /\d/);
  const seekedAt = Number(await video.getAttribute('data-seeked-at'));
  expect(seekedAt).toBeGreaterThan(readyDemo.chapters[1].startSeconds - 0.5);
  expect(seekedAt).toBeLessThan(readyDemo.chapters[1].startSeconds + 0.5);
});

// The design (SHIP-1810 P2) keeps the action last in the band, so the reason reads before the disabled button.
test('DemoStatusBand_WhenActionDisabledOnAPhone_ShouldShowTheReasonAboveIt', async ({
  mount,
  page,
}) => {
  await stubDemo(page, blocked);
  await mount(view);
  const action = page.getByTestId(ids.demoCreateBlocked);
  const reason = page.getByTestId(ids.demoCreateReason);

  await expect(action).toBeDisabled();
  await expect(reason).toBeVisible();
  await expect(reason).toHaveText(blocked.actions.createBlockedMessage!);
  const above = (await reason.boundingBox())!;
  const button = (await action.boundingBox())!;
  expect(button.y).toBeGreaterThanOrEqual(above.y + above.height);
  expect(await action.getAttribute('title')).toBeNull();
});

test('DemoStatusBand_WhenClosableOnAPhone_ShouldKeepCreateOffInItsPlace', async ({
  mount,
  page,
}) => {
  const closable: SprintDemoDto = {
    ...(none as unknown as SprintDemoDto),
    status: 'closable',
    actions: { canCreate: false, createBlockedMessage: null, canRetryYouTube: false },
  };
  await stubDemo(page, closable);
  await mount(view);
  const action = page.getByTestId(ids.demoCreateBlocked);

  await expect(action).toBeDisabled();
  const band = (await page.getByTestId(ids.demoStatusBand).boundingBox())!;
  const button = (await action.boundingBox())!;
  const message = (await page.getByTestId(ids.demoStatusMessage).boundingBox())!;
  expect(button.width).toBeGreaterThan(band.width - 12 * 2 - 4 - 1);
  expect(button.y).toBeGreaterThanOrEqual(message.y + message.height);
});

test('DemoStatusBand_WhenRunOnAPhone_ShouldShowTheItemAsAFullWidthRow', async ({ mount, page }) => {
  const demo = working as unknown as SprintDemoDto;
  await stubDemo(page, demo);
  await mount(view);
  const item = page.getByTestId(ids.demoItemLink);
  // The type icon's letter, the key, the title, then the › (ND4-5).
  await expect(item).toHaveText(`U${demo.run!.itemKey}${demo.run!.itemTitle}›`);
  await expect(item.getByTestId(componentTestIds.typeIcon)).toHaveAttribute(
    'data-type',
    demo.run!.itemType,
  );

  const band = (await page.getByTestId(ids.demoStatusBand).boundingBox())!;
  const row = (await item.boundingBox())!;
  const message = (await page.getByTestId(ids.demoStatusMessage).boundingBox())!;
  expect(row.width).toBeGreaterThan(band.width - 12 * 2 - 4 - 1);
  expect(row.height).toBeGreaterThanOrEqual(32);
  expect(row.y).toBeGreaterThanOrEqual(message.y + message.height);
});

test('DemoYouTubeNotice_WhenFailedOnAPhone_ShouldSitUnderThePlayer', async ({ mount, page }) => {
  const demo = youtubeFailed as unknown as SprintDemoDto;
  await stubDemo(page, demo);
  await mount(view);
  const notice = page.getByTestId(ids.demoYoutubeNotice);
  await expect(notice).toBeVisible();

  // Under the player and above its facts (Length | Size, File, Delivered), as the design draws it (ND4-8).
  const player = (await page.getByTestId(ids.demoVideo).boundingBox())!;
  const box = (await notice.boundingBox())!;
  const facts = (await page.getByTestId(ids.demoFacts).boundingBox())!;
  expect(box.y).toBeGreaterThanOrEqual(player.y + player.height);
  expect(box.y + box.height).toBeLessThanOrEqual(facts.y);
});

test('SprintDemoPanel_WhenOnAPhone_ShouldShowTheSprintNumbersInOneRowAboveTheBand', async ({
  mount,
  page,
}) => {
  const demo = working as unknown as SprintDemoDto;
  await stubDemo(page, demo);
  await mount(view);
  const committed = page.getByTestId(ids.demoNumberCommitted);
  const completed = page.getByTestId(ids.demoNumberCompleted);
  const moved = page.getByTestId(ids.demoNumberFeaturesMoved);
  await expect(committed).toHaveText(`${demo.numbers.committed}`);
  await expect(completed).toHaveText(`${demo.numbers.completed}`);
  await expect(moved).toHaveText(`${demo.numbers.featuresMoved}`);
  await expect(page.getByTestId(ids.demoNumbers)).toContainText('Features moved');

  const row = (await page.getByTestId(ids.demoNumbers).boundingBox())!;
  const band = (await page.getByTestId(ids.demoStatusBand).boundingBox())!;
  const tops = await Promise.all(
    [committed, completed, moved].map(async (n) => (await n.boundingBox())!.y),
  );
  expect(new Set(tops).size).toBe(1);
  expect(row.y + row.height).toBeLessThanOrEqual(band.y);
});

test('DemoStatusBand_WhenTheSessionEndedWithWordsOnAPhone_ShouldQuoteThemUnderTheMessage', async ({
  mount,
  page,
}) => {
  const base = working as unknown as SprintDemoDto;
  const demo: SprintDemoDto = {
    ...base,
    status: 'failed',
    statusMessage:
      'The agent session ended as failed before a video came back. It said: "No seeded copy."',
    agentSaid: {
      message: 'The agent session ended as failed before a video came back.',
      text: 'No seeded copy.',
    },
    run: { ...base.run!, sessionState: 'ended' },
    actions: { canCreate: true, createBlockedMessage: null, canRetryYouTube: false },
  };
  await stubDemo(page, demo);
  await mount(view);
  const message = page.getByTestId(ids.demoStatusMessage);
  const said = page.getByTestId(ids.demoAgentSaid);
  await expect(message).toHaveText(demo.agentSaid!.message);
  await expect(said).toHaveText(`It said${demo.agentSaid!.text}`);
  // The line under the message names who asked (ND4-10).
  await expect(page.getByTestId(ids.demoStatusBand)).toContainText(demo.run!.requestedByLabel!);

  const messageBox = (await message.boundingBox())!;
  const saidBox = (await said.boundingBox())!;
  const item = (await page.getByTestId(ids.demoItemLink).boundingBox())!;
  expect(saidBox.y).toBeGreaterThanOrEqual(messageBox.y + messageBox.height);
  expect(saidBox.y + saidBox.height).toBeLessThanOrEqual(item.y);
});

test('DemoStatusBand_WhenOnAPhone_ShouldPutTheActionFullWidth', async ({ mount, page }) => {
  await stubDemo(page, none as unknown as SprintDemoDto);
  await mount(view);
  const action = page.getByTestId(ids.demoCreate);
  await expect(action).toBeEnabled();

  const band = (await page.getByTestId(ids.demoStatusBand).boundingBox())!;
  const button = (await action.boundingBox())!;
  const message = (await page.getByTestId(ids.demoStatusMessage).boundingBox())!;
  // Inside the band's padding and 3px edge: the button spans it, under the message.
  expect(button.width).toBeGreaterThan(band.width - 12 * 2 - 4 - 1);
  expect(button.height).toBeGreaterThanOrEqual(32);
  expect(button.y).toBeGreaterThanOrEqual(message.y + message.height);
});
