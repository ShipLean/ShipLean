import { expect, test } from '@playwright/experimental-ct-react';
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import unlisted from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-unlisted.json' with { type: 'json' };
import privateDemo from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-private.json' with { type: 'json' };
import failed from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-failed.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { DemoPlayerHarness } from './ct/DemoPlayerHarness';
import { demosTestIds as ids } from './testIds';

const demos = { ready, unlisted, privateDemo, failed } as unknown as Record<string, SprintDemoDto>;

test('Player_WhenVideo_ShouldUseContentUrl', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demos.ready} />);

  const video = component.getByTestId(ids.demoVideo);
  await expect(video).toHaveAttribute('src', /\/assets\/demo-fixture-.*\.mp4$/);
  await expect(video).toHaveAttribute('controls', '');
  await expect(component.getByTestId(ids.demoEmbed)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoWatchOnYoutube)).toHaveCount(0);
});

test('Player_WhenNoVideo_ShouldRenderNothing', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={{ ...demos.ready, video: null }} />);

  await expect(component.getByTestId(ids.demoChapters)).toBeVisible();
  await expect(component.getByTestId(ids.demoPlayer)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoVideo)).toHaveCount(0);
});

test('Embed_WhenUnlisted_ShouldUseNoCookieUrlAndReferrerPolicy', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demos.unlisted} />);

  const embed = component.getByTestId(ids.demoEmbed);
  await expect(embed).toHaveAttribute(
    'src',
    /^https:\/\/www\.youtube-nocookie\.com\/embed\/[\w-]+$/,
  );
  await expect(embed).toHaveAttribute('referrerpolicy', 'strict-origin-when-cross-origin');
  await expect(embed).toHaveAttribute('allowfullscreen', '');
  await expect(component.getByTestId(ids.demoVideo)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoWatchOnYoutube)).toHaveAttribute(
    'href',
    'https://www.youtube.com/watch?v=abc123XYZ_-',
  );
  await expect(component.getByTestId(ids.demoPrivateNote)).toHaveCount(0);
});

test('Private_ShouldPlayTheVideoAndLinkToYouTubeWithTheNote', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demos.privateDemo} />);

  await expect(component.getByTestId(ids.demoVideo)).toBeVisible();
  await expect(component.getByTestId(ids.demoEmbed)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoWatchOnYoutube)).toHaveAttribute(
    'href',
    'https://www.youtube.com/watch?v=abc123XYZ_-',
  );
  await expect(component.getByTestId(ids.demoPrivateNote)).toContainText(
    'only the channel owner can watch it there',
  );
});

test('Pending_ShouldPlayTheVideoAndSaySending', async ({ mount }) => {
  const pending = {
    ...demos.ready,
    youTube: { ...demos.ready.youTube, state: 'pending' },
  } as SprintDemoDto;
  const component = await mount(<DemoPlayerHarness demo={pending} />);

  await expect(component.getByTestId(ids.demoVideo)).toBeVisible();
  await expect(component.getByTestId(ids.demoYoutubeNotice)).toContainText('Sending to YouTube');
  await expect(component.getByTestId(ids.demoYoutubeRetry)).toHaveCount(0);
});

test('Failed_ShouldPlayTheVideoAndSayWhy', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demos.failed} />);

  await expect(component.getByTestId(ids.demoVideo)).toBeVisible();
  await expect(component.getByTestId(ids.demoYoutubeNotice)).toContainText(
    'YouTube refused the upload: the daily quota is used up.',
  );
  await expect(component.getByTestId(ids.demoYoutubeRetry)).toBeVisible();
});

// Baselines come from CI's image (SH1 owns them); until then these have none and are red.
for (const [name, key] of [
  ['ready', 'ready'],
  ['youtube-unlisted', 'unlisted'],
  ['youtube-private', 'privateDemo'],
  ['youtube-failed', 'failed'],
] as const) {
  for (const theme of ['light', 'dark'] as const) {
    test(`Player_WhenRendered_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const component = await mount(<DemoPlayerHarness demo={demos[key]} />);
      await expect(component.getByTestId(ids.demoPlayer)).toBeVisible();
      await expect(component).toHaveScreenshot(`demo-${name}.${theme}.png`, {
        animations: 'disabled',
        mask: [component.getByTestId(ids.demoVideo), component.getByTestId(ids.demoEmbed)],
      });
    });
  }
}
