import { expect } from '@playwright/experimental-ct-react';
import { demosTestIds as ids } from '../../features/demos/testIds';
import type { SprintDemoDto } from '@shiplean/core/api/demos';
import { demoSprints, demoStates, stubDemo } from './fixtures/demos';
import { stubSprintFigures } from './fixtures/sprints';
import { stubShell } from './shellStub';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';

// The Sprint Demo tab on a phone (SHIP-889, L889a): the page in every status, light and dark. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/demos.phone.spec.tsx. The desktop twins are in demos.spec.tsx.

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
    await stubShell(page, { sprints: demoSprints });
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

phonePageShots([
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
