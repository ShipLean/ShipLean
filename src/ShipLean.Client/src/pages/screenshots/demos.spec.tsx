import { expect } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { demosTestIds as ids } from '../../features/demos/testIds';
import { workItemTestIds } from '../../features/workItem/testIds';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubSide } from '../../features/workItem/ct/stubSide';
import type { SprintDemoDto } from '../../api/demos';
import { bauItem, demoStates, settingsStates, stubDemo, stubDemoSettings } from './fixtures/demos';
import { stubSprintFigures } from './fixtures/sprints';
import { projectSettings } from './fixtures/workItemSettings';
import { attachments, comments, revisions, workItemStates } from './fixtures/workItem';
import { PEOPLE, SHELL_MEMBERS, SHELL_SPRINTS, stubShell } from './shellStub';
import { pageShots, type Page } from './shoot';

// ---- The Demo tab: one shot per status (design/screens/sprint-demo*.png) ----

const demoShot = (
  name: string,
  demo: SprintDemoDto,
  ready: (page: Page) => Promise<void> = (page) =>
    expect(page.getByTestId(ids.demoStatusBand)).toHaveAttribute('data-status', demo.status),
) => ({
  name,
  route: `/p/SHIP/sprints/${demo.sprintId}/demo`,
  shell: true,
  arrange: async (page: Page) => {
    await stubShell(page);
    await stubSprintFigures(page);
    await stubDemo(page, demo);
  },
  ready,
});

const withPlayer = (demo: SprintDemoDto, player: 'video' | 'embed') => async (page: Page) => {
  await expect(page.getByTestId(ids.demoStatusBand)).toHaveAttribute('data-status', demo.status);
  await expect(page.getByTestId(player === 'video' ? ids.demoVideo : ids.demoEmbed)).toBeVisible();
  await expect(page.getByTestId(ids.demoChapter)).toHaveCount(demo.chapters.length);
  await expect(page.getByTestId(ids.demoScript)).toBeVisible();
};

pageShots([
  demoShot('sprint-demo-closable', demoStates.closable),
  demoShot('sprint-demo-none', demoStates.none),
  demoShot('sprint-demo-blocked', demoStates.blocked),
  demoShot('sprint-demo-requested', demoStates.requested),
  demoShot('sprint-demo-working', demoStates.working),
  demoShot('sprint-demo-failed', demoStates.failed),
  demoShot('sprint-demo', demoStates.ready, withPlayer(demoStates.ready, 'video')),
  demoShot(
    'sprint-demo-youtube-unlisted',
    demoStates.youtubeUnlisted,
    withPlayer(demoStates.youtubeUnlisted, 'embed'),
  ),
  demoShot(
    'sprint-demo-youtube-private',
    demoStates.youtubePrivate,
    withPlayer(demoStates.youtubePrivate, 'video'),
  ),
  demoShot('sprint-demo-youtube-failed', demoStates.youtubeFailed, async (page) => {
    await withPlayer(demoStates.youtubeFailed, 'video')(page);
    await expect(page.getByTestId(ids.demoYoutubeNotice)).toBeVisible();
  }),
]);

// ---- The Demos settings tab, one shot per YouTube state and per brand state (design/screens/settings-demos*.png) ----

const settingsShot = (
  name: string,
  settings: (typeof settingsStates)[keyof typeof settingsStates],
  ready: (page: Page) => Promise<void>,
  act?: (page: Page) => Promise<void>,
) => ({
  name,
  route: '/p/SHIP/settings/demos',
  shell: true,
  arrange: async (page: Page) => {
    await stubShell(page);
    await page.route('**/api/projects/p1/settings', (route) =>
      route.fulfill({ json: projectSettings }),
    );
    await stubDemoSettings(page, settings);
  },
  ready,
  act,
});

const panelsReady = async (page: Page) => {
  await expect(page.getByTestId(ids.demoSettingsPanel)).toBeVisible();
  await expect(page.getByTestId(ids.demoBrandPanel)).toBeVisible();
  await expect(page.getByTestId(ids.youtubePanel)).toBeVisible();
};

const refuseThenDismiss = async (page: Page, shown: string, message: string) => {
  await expect(page.getByTestId(shown)).toHaveText(message);
  // The toast would time out in the middle of the comparison: dismiss it so the shot is the same each run.
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(message);
  await page.getByTestId(componentTestIds.toastDismiss).click();
  await expect(page.getByTestId(componentTestIds.toast)).toHaveCount(0);
};

const LOGO_REFUSAL = 'A logo must be a PNG, JPEG, WebP or SVG image.';
const TOKENS_REFUSAL = 'Not saved. Line 1: --color-bg has a value that is not allowed.';

pageShots([
  settingsShot('settings-demos', settingsStates.off, panelsReady),
  settingsShot('settings-demos-logo', settingsStates.branded, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.demoLogoPreview)).toHaveJSProperty('naturalWidth', 640);
  }),
  {
    ...settingsShot(
      'settings-demos-logo-refused',
      settingsStates.off,
      panelsReady,
      async (page) => {
        await page.route('**/api/projects/*/demo-brand/logo', (route) =>
          route.fulfill({
            status: 400,
            json: { error: LOGO_REFUSAL, code: 'validation', field: 'logo' },
          }),
        );
        await page.getByTestId(ids.demoLogoFile).setInputFiles({
          name: 'brand-guidelines.pdf',
          mimeType: 'application/pdf',
          buffer: Buffer.from('%PDF-1.4'),
        });
        await refuseThenDismiss(page, ids.demoLogoError, LOGO_REFUSAL);
      },
    ),
  },
  settingsShot('settings-demos-tokens-refused', settingsStates.off, panelsReady, async (page) => {
    await page.route('**/api/projects/*/demo-brand/tokens', (route) =>
      route.fulfill({
        status: 400,
        json: { error: TOKENS_REFUSAL, code: 'validation', field: 'tokens' },
      }),
    );
    await page.getByTestId(ids.demoTokens).fill('--color-bg: url(https://x.example/p.png);');
    await page.getByTestId(ids.demoTokensSave).click();
    await refuseThenDismiss(page, ids.demoTokensError, TOKENS_REFUSAL);
  }),
  settingsShot('settings-demos-yt-off', settingsStates.off, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.youtubeOff)).toBeVisible();
  }),
  settingsShot('settings-demos-yt-connect', settingsStates.notConnected, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.youtubeConnect)).toBeVisible();
  }),
  settingsShot('settings-demos-yt-pending', settingsStates.pending, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.youtubeChannel)).toBeVisible();
    await expect(page.getByTestId(ids.youtubeCheck)).toBeVisible();
  }),
  settingsShot('settings-demos-yt-refused', settingsStates.refused, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.youtubeRefusal)).toBeVisible();
  }),
  settingsShot('settings-demos-yt-connected', settingsStates.branded, async (page) => {
    await panelsReady(page);
    await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  }),
]);

// ---- The BAU item a demo runs as: a work item on no sprint, board or parent ----

pageShots([
  {
    name: 'work-item-bau',
    route: `/p/SHIP/items/${bauItem.key}`,
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubApi(page, bauItem);
      // The form fixture's states, members and sprints are registered last and win; put the shell's back.
      await page.route('**/api/projects/p1/states', (route) =>
        route.fulfill({ json: workItemStates }),
      );
      await page.route('**/api/projects/p1/members', (route) =>
        route.fulfill({ json: SHELL_MEMBERS }),
      );
      await page.route('**/api/projects/p1/sprints', (route) =>
        route.fulfill({ json: SHELL_SPRINTS }),
      );
      await page.route('**/api/work-items/*/comments', (route) =>
        route.fulfill({ json: comments }),
      );
      await stubSide(page, PEOPLE.priya, attachments, revisions);
    },
    ready: async (page) => {
      await expect(page.getByTestId(workItemTestIds.title)).toHaveValue(bauItem.title);
      await expect(page.getByTestId(workItemTestIds.attachment)).toHaveCount(attachments.length);
    },
  },
]);
