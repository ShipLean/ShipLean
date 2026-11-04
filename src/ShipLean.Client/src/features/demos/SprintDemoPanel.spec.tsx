import { expect, test } from '@playwright/experimental-ct-react';
import none from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { realtimeTestIds } from '../realtime/testIds';
import { stubShell } from '../../pages/screenshots/shellStub';
import { stubHub } from '../retros/ct/retroFixtures';
import { DemoPanelHarness } from './ct/DemoPanelHarness';
import { demosTestIds as ids } from './testIds';

const project = { id: 'p1', key: 'SHIP', role: 'member' } as ProjectDto;
const sprint = { id: none.sprintId, name: 'Sprint 24' } as SprintDto;
const view = <DemoPanelHarness project={project} sprint={sprint} />;

test('Panel_WhenNone_ShouldShowCreate', async ({ mount, page }) => {
  await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: none }));
  const component = await mount(view);

  await expect(component.getByTestId(ids.demoStatusBand)).toBeVisible();
  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
  await expect(component.getByTestId(ids.demoVideo)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoScript)).toHaveCount(0);
});

test('Panel_WhenFailedWithAnOldVideo_ShouldKeepShowingVideoAndScript', async ({ mount, page }) => {
  const failedWithVideo = { ...ready, status: 'failed', statusMessage: 'The agent stopped.' };
  await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: failedWithVideo }));
  const component = await mount(view);

  await expect(component.getByTestId(ids.demoStatusBand)).toBeVisible();
  await expect(component.getByTestId(ids.demoVideo)).toBeVisible();
  await expect(component.getByTestId(ids.demoScript)).toBeVisible();
});

test('Panel_WhenReady_ShouldMountTheVideoWithContentUrl', async ({ mount, page }) => {
  await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: ready }));
  const component = await mount(view);

  const video = component.getByTestId(ids.demoVideo);
  await expect(video).toHaveAttribute('src', ready.video.contentUrl);
  await expect(video).toHaveAttribute('controls', '');
});

test('Panel_WhenLoadFails_ShouldShowErrorWithRetry', async ({ mount, page }) => {
  let reads = 0;
  await page.route('**/api/sprints/*/demo', (route) => {
    reads += 1;
    return reads === 1
      ? route.fulfill({
          status: 500,
          json: { error: 'The demo could not be read.', code: 'internal_error' },
        })
      : route.fulfill({ json: none });
  });
  const component = await mount(view);

  await expect(component).toContainText('The demo could not be read.');
  await expect(component.getByTestId(ids.demoStatusBand)).toHaveCount(0);

  await component.getByTestId(ids.demoLoadRetry).click();

  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
  expect(reads).toBe(2);
});

test('Panel_WhenEventArrives_ShouldRefetch', async ({ mount, page }) => {
  await stubShell(page);
  let reads = 0;
  await page.route('**/api/sprints/*/demo', (route) => {
    reads += 1;
    return route.fulfill({ json: none });
  });
  const hub = await stubHub(page);
  const component = await mount(view);
  await expect(component.getByTestId(ids.demoStatusBand)).toBeVisible();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute('data-state', 'live');
  const before = reads;

  // Two events of other sprints first, then this one's: events arrive in order, so once this one has refetched the other
  // has been handled too, and the count is exactly one more.
  for (const other of ['other-1', 'other-2']) {
    await hub.push({ type: 'sprintDemoChanged', projectId: 'p1', clientId: null, sprintId: other });
  }
  await hub.push({
    type: 'sprintDemoChanged',
    projectId: 'p1',
    clientId: null,
    sprintId: none.sprintId,
  });

  await expect.poll(() => reads).toBe(before + 1);
});

for (const width of [1280, 1440]) {
  test(`Panel_WhenReady_ShouldFitTheWindowAt${width}`, async ({ mount, page }) => {
    await page.setViewportSize({ width, height: 800 });
    await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: ready }));
    const component = await mount(view);
    await expect(component.getByTestId(ids.demoVideo)).toBeVisible();

    const fit = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      view: window.innerWidth,
    }));
    expect(fit.scroll).toBeLessThanOrEqual(fit.view);
    const band = await component.getByTestId(ids.demoStatusBand).boundingBox();
    expect(band!.x + band!.width).toBeLessThanOrEqual(width);
  });
}

// A tall demo: many chapters and features must scroll inside their own panels, not push the band or its buttons out.
const tall = {
  ...ready,
  chapters: Array.from({ length: 60 }, (_, index) => ({
    title: `Chapter ${index} ${'long '.repeat(10)}`,
    startSeconds: index,
    startLabel: `0:${String(index).padStart(2, '0')}`,
    featureKey: null,
  })),
  features: Array.from({ length: 60 }, (_, index) => ({
    ...ready.features[0],
    key: `SHIP-${300 + index}`,
    url: `/p/SHIP/items/SHIP-${300 + index}`,
  })),
};

for (const width of [1280, 1440]) {
  test(`Panel_WhenListsAreTall_ShouldKeepBandAndButtonsInTheWindowAt${width}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: tall }));
    const component = await mount(view);
    await expect(component.getByTestId(ids.demoChapter)).toHaveCount(60);

    const view_ = await page.evaluate(() => ({ h: window.innerHeight, w: window.innerWidth }));
    for (const id of [ids.demoStatusBand, ids.demoCreate]) {
      const box = await component.getByTestId(id).boundingBox();
      expect(box!.y).toBeGreaterThanOrEqual(0);
      expect(box!.y + box!.height).toBeLessThanOrEqual(view_.h);
      expect(box!.x + box!.width).toBeLessThanOrEqual(view_.w);
    }
    // The chapter list scrolls inside its panel instead of growing the page without bound.
    const list = await component
      .getByTestId(ids.demoChapters)
      .evaluate((el) => ({ client: el.clientHeight, scroll: el.scrollHeight }));
    expect(list.client).toBeLessThan(view_.h);
    expect(list.scroll).toBeGreaterThan(list.client);
  });
}
