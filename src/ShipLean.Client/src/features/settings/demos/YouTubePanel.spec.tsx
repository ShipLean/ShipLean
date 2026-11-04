import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import settings from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import disconnectFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/youtube-disconnect.json' with { type: 'json' };
import type { YouTubeProjectDto } from '../../../api/demos';
import type { ProjectDto } from '../../../api/projects';
import { componentTestIds } from '../../../components/testIds';
import { demosTestIds as ids } from '../../demos/testIds';
import { harnessLocationTestId } from './youtube/ct/harnessIds';
import { YouTubePanelHarness } from './youtube/ct/YouTubePanelHarness';

const admin = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
const member = { ...admin, role: 'member' } as ProjectDto;
const hooks = (query = '') => ({ hooksConfig: { route: `/p/SHIP/settings/demos${query}` } });

const off: YouTubeProjectDto = {
  status: 'off',
  channelTitle: null,
  visibility: null,
  checkedLabel: null,
  refusal: null,
};
const notConnected: YouTubeProjectDto = { ...off, status: 'notConnected' };
const pending: YouTubeProjectDto = {
  ...off,
  status: 'pending',
  channelTitle: 'Storefront Official',
};
const connected: YouTubeProjectDto = {
  ...pending,
  status: 'connected',
  visibility: 'unlisted',
  checkedLabel: 'Checked 4 Nov 2026, 14:02',
};
const refusal =
  "YouTube gave the test video private visibility, not the unlisted you chose. The channel or its account settings decide this. Change the channel's setting and check again, choose private, or don't use YouTube.";

// The server as the panel sees it: the settings read answers with `server.youTube`, and the writes change it as the real ones do.
interface Server {
  youTube: YouTubeProjectDto;
  starts: number;
  checks: unknown[];
  disconnects: number;
}

async function serve(page: Page, youTube: YouTubeProjectDto): Promise<Server> {
  const server: Server = { youTube, starts: 0, checks: [], disconnects: 0 };
  await page.route('**/api/projects/*/demo-settings', (route) =>
    route.fulfill({ json: { ...settings, youTube: server.youTube } }),
  );
  await page.route('**/api/projects/*/youtube/start', (route) => {
    server.starts += 1;
    return route.fulfill({ json: { url: 'http://accounts.example.test/auth?state=s1' } });
  });
  await page.route('**/api/projects/*/youtube/disconnect', (route) => {
    server.disconnects += 1;
    server.youTube = notConnected;
    return route.fulfill({ json: { revoked: true, message: null } });
  });
  return server;
}

const radio = (page: Page, name: string) =>
  page.getByTestId(ids.youtubeVisibility).getByLabel(name, { exact: true });

test('Panel_WhenOff_ShouldSayAnAdminSetsItUp', async ({ mount, page }) => {
  await serve(page, off);
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await expect(page.getByTestId(ids.youtubeOff)).toContainText("YouTube isn't set up");
  await expect(page.getByTestId(ids.youtubePanel)).toContainText('An instance admin turns it on');
  await expect(page.getByTestId(ids.youtubeConnect)).toHaveCount(0);
  await expect(page.getByTestId(ids.youtubeCheck)).toHaveCount(0);
});

test('Panel_WhenNotConnected_ShouldOfferConnect', async ({ mount, page }) => {
  await serve(page, notConnected);
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await expect(page.getByTestId(ids.youtubeConnect)).toBeEnabled();
  await expect(page.getByTestId(ids.youtubePanel)).toContainText(
    'Nothing is saved as connected until the check passes',
  );
  await expect(page.getByTestId(ids.youtubeOff)).toHaveCount(0);
  await expect(page.getByTestId(ids.youtubeCheck)).toHaveCount(0);
});

test('Connect_ShouldNavigateToTheUrl', async ({ mount, page }) => {
  const server = await serve(page, notConnected);
  await page.route('http://accounts.example.test/**', (route) =>
    route.fulfill({ contentType: 'text/html', body: '<p>Google</p>' }),
  );
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await page.getByTestId(ids.youtubeConnect).click();

  await page.waitForURL('http://accounts.example.test/auth?state=s1');
  expect(server.starts).toBe(1);
});

for (const [kind, text] of [
  ['denied', 'You did not allow ShipLean'],
  ['error', 'could not connect that channel and saved nothing'],
  ['pending', 'Signed in to Google'],
] as const) {
  test(`Return_When${kind[0].toUpperCase()}${kind.slice(1)}_ShouldSayAndClearTheQuery`, async ({
    mount,
    page,
  }) => {
    await serve(page, kind === 'pending' ? pending : notConnected);
    await mount(<YouTubePanelHarness project={admin} />, hooks(`?youtube=${kind}`));

    await expect(page.getByTestId(ids.youtubePanel)).toContainText(text);
    await expect(page.getByTestId(harnessLocationTestId)).toHaveText('/p/SHIP/settings/demos');
  });
}

test('Return_WhenDenied_ShouldKeepOtherQueryValues', async ({ mount, page }) => {
  await serve(page, notConnected);
  await mount(<YouTubePanelHarness project={admin} />, hooks('?youtube=denied&x=1'));

  await expect(page.getByTestId(ids.youtubePanel)).toContainText('You did not allow ShipLean');
  await expect(page.getByTestId(harnessLocationTestId)).toHaveText('/p/SHIP/settings/demos?x=1');
});

test('Pending_ShouldRequireAChoiceBeforeCheck', async ({ mount, page }) => {
  const server = await serve(page, pending);
  await page.route('**/api/projects/*/youtube/check', (route) => {
    server.checks.push(route.request().postDataJSON());
    server.youTube = { ...connected, visibility: 'unlisted' };
    return route.fulfill({ json: server.youTube });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  for (const name of ['Public', 'Unlisted', 'Private']) {
    await expect(radio(page, name)).not.toBeChecked();
  }
  await expect(page.getByTestId(ids.youtubeCheck)).toBeDisabled();
  await expect(page.getByTestId(ids.youtubeConnected)).toHaveCount(0);

  await radio(page, 'Unlisted').check();
  await expect(page.getByTestId(ids.youtubeCheck)).toBeEnabled();
  await page.getByTestId(ids.youtubeCheck).click();

  await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  expect(server.checks).toEqual([{ visibility: 'unlisted' }]);
});

test('Check_ShouldWarnAboutQuotaAndDisableWhileRunning', async ({ mount, page }) => {
  const server = await serve(page, pending);
  let release!: () => void;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/projects/*/youtube/check', async (route) => {
    server.checks.push(route.request().postDataJSON());
    await gate;
    server.youTube = connected;
    return route.fulfill({ json: connected });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  const panel = page.getByTestId(ids.youtubePanel);
  await expect(panel).toContainText('Uploads a 2-second test video');
  await expect(panel).toContainText('then deletes it');
  await expect(panel).toContainText("one of the Google project's 100 uploads a day");

  await radio(page, 'Unlisted').check();
  await page.getByTestId(ids.youtubeCheck).click();
  await expect(page.getByTestId(ids.youtubeCheck)).toBeDisabled();
  await expect(page.getByTestId(ids.youtubeCheck)).toHaveText('Checking…');
  await expect(radio(page, 'Private')).toBeDisabled();
  await expect(page.getByTestId(ids.youtubeDisconnect)).toBeDisabled();
  expect(server.checks).toHaveLength(1);

  release();
  await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  await expect(page.getByTestId(ids.youtubeCheck)).toBeEnabled();
});

test('Refused_ShouldShowTheServersReasonAndKeepTheChoices', async ({ mount, page }) => {
  const server = await serve(page, pending);
  await page.route('**/api/projects/*/youtube/check', (route) => {
    const body = route.request().postDataJSON() as { visibility: string };
    server.checks.push(body);
    if (body.visibility === 'private') {
      server.youTube = connected;
      return route.fulfill({ json: { ...connected, visibility: 'private' } });
    }
    server.youTube = { ...pending, refusal };
    return route.fulfill({ json: server.youTube });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await radio(page, 'Unlisted').check();
  await page.getByTestId(ids.youtubeCheck).click();

  await expect(page.getByTestId(ids.youtubeRefusal)).toHaveText(refusal);
  await expect(page.getByTestId(ids.youtubeConnected)).toHaveCount(0);
  // The radios stay, with the choice that was refused, and the check can be run again.
  await expect(radio(page, 'Unlisted')).toBeChecked();
  await expect(page.getByTestId(ids.youtubeCheck)).toBeEnabled();
  await expect(page.getByTestId(ids.youtubeDisconnect)).toBeVisible();

  await radio(page, 'Private').check();
  await page.getByTestId(ids.youtubeCheck).click();
  await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  await expect(page.getByTestId(ids.youtubeRefusal)).toHaveCount(0);
  expect(server.checks).toEqual([{ visibility: 'unlisted' }, { visibility: 'private' }]);
});

test('Recheck_WhenRefused_ShouldSetAConnectedChannelBackToPending', async ({ mount, page }) => {
  const server = await serve(page, connected);
  await page.route('**/api/projects/*/youtube/check', (route) => {
    server.checks.push(route.request().postDataJSON());
    server.youTube = { ...pending, refusal };
    return route.fulfill({ json: server.youTube });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  // A different choice on a connected channel runs the check at once.
  await radio(page, 'Public').check();

  await expect(page.getByTestId(ids.youtubeRefusal)).toHaveText(refusal);
  await expect(page.getByTestId(ids.youtubeConnected)).toHaveCount(0);
  await expect(page.getByTestId(ids.youtubePanel)).toContainText('Pending check');
  await expect(page.getByTestId(ids.youtubeCheck)).toHaveText('Check and connect');
  expect(server.checks).toEqual([{ visibility: 'public' }]);
});

test('Connected_WhenTheTestVideoCouldNotBeDeleted_ShouldStayConnectedAndNameTheVideo', async ({
  mount,
  page,
}) => {
  const server = await serve(page, pending);
  const named =
    'The test video abc123 could not be deleted from the channel (YouTube said no.) Delete it in YouTube Studio, then check again.';
  await page.route('**/api/projects/*/youtube/check', (route) => {
    server.youTube = connected;
    return route.fulfill({ json: { ...connected, refusal: named } });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await radio(page, 'Unlisted').check();
  await page.getByTestId(ids.youtubeCheck).click();

  await expect(page.getByTestId(ids.youtubeConnected)).toBeVisible();
  await expect(page.getByTestId(ids.youtubePanel)).toContainText(named);
  // It is not a refusal: the channel connected.
  await expect(page.getByTestId(ids.youtubeRefusal)).toHaveCount(0);
});

test('Connected_ShouldShowChannelAndVisibilityAndDisconnect', async ({ mount, page }) => {
  await serve(page, connected);
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await expect(page.getByTestId(ids.youtubeConnected)).toHaveText('Connected');
  await expect(page.getByTestId(ids.youtubeChannel)).toHaveText('Storefront Official');
  await expect(radio(page, 'Unlisted')).toBeChecked();
  await expect(radio(page, 'Public')).not.toBeChecked();
  await expect(page.getByTestId(ids.youtubePanel)).toContainText(
    'Passed: YouTube kept the test video unlisted.',
  );
  await expect(page.getByTestId(ids.youtubePanel)).toContainText('Checked 4 Nov 2026, 14:02');
  await expect(page.getByTestId(ids.youtubeCheck)).toHaveText('Check again');
  await expect(page.getByTestId(ids.youtubeDisconnect)).toBeVisible();
  await expect(page.getByTestId(ids.youtubeRefusal)).toHaveCount(0);
});

test('Disconnect_WhenConfirmed_ShouldCall', async ({ mount, page }) => {
  const server = await serve(page, connected);
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await page.getByTestId(ids.youtubeDisconnect).click();
  // The first press only asks.
  await expect(page.getByTestId(ids.youtubeDisconnect)).toHaveText('Yes, disconnect');
  expect(server.disconnects).toBe(0);

  await page.getByTestId(ids.youtubeDisconnect).click();

  await expect(page.getByTestId(ids.youtubeConnect)).toBeVisible();
  expect(server.disconnects).toBe(1);
  await expect(page.getByTestId(ids.youtubeConnected)).toHaveCount(0);
});

test('Disconnect_WhenNotRevoked_ShouldSayToRemoveAccessInGoogle', async ({ mount, page }) => {
  const server = await serve(page, connected);
  await page.route('**/api/projects/*/youtube/disconnect', (route) => {
    server.disconnects += 1;
    server.youTube = notConnected;
    return route.fulfill({ json: disconnectFixture });
  });
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await page.getByTestId(ids.youtubeDisconnect).click();
  await page.getByTestId(ids.youtubeDisconnect).click();

  await expect(page.getByTestId(ids.youtubeConnect)).toBeVisible();
  await expect(page.getByTestId(ids.youtubePanel)).toContainText(disconnectFixture.message);
  await expect(page.getByTestId(ids.youtubePanel)).toContainText('Remove ShipLean');
});

test('ReadOnly_WhenMember_ShouldHideActions', async ({ mount, page }) => {
  for (const state of [notConnected, pending, connected]) {
    await serve(page, state);
    const view = await mount(<YouTubePanelHarness project={member} />, hooks());
    await expect(page.getByTestId(ids.youtubePanel)).toContainText(
      state.status === 'notConnected' ? 'Not connected' : 'Storefront Official',
    );
    for (const id of [ids.youtubeConnect, ids.youtubeCheck, ids.youtubeDisconnect]) {
      await expect(page.getByTestId(id)).toHaveCount(0);
    }
    if (state.status !== 'notConnected') {
      for (const name of ['Public', 'Unlisted', 'Private']) {
        await expect(radio(page, name)).toBeDisabled();
      }
    }
    await view.unmount();
    await page.unroute('**/api/projects/*/demo-settings');
  }
});

test('Check_WhenRequestFails_ShouldToast', async ({ mount, page }) => {
  await serve(page, pending);
  await page.route('**/api/projects/*/youtube/check', (route) =>
    route.fulfill({
      status: 409,
      json: { error: 'There is no YouTube channel to check.', code: 'conflict' },
    }),
  );
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await radio(page, 'Unlisted').check();
  await page.getByTestId(ids.youtubeCheck).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'There is no YouTube channel to check.',
  );
  await expect(page.getByTestId(ids.youtubeCheck)).toBeEnabled();
  await expect(page.getByTestId(ids.youtubeConnected)).toHaveCount(0);
});

test('Connect_WhenRequestFails_ShouldToastAndStay', async ({ mount, page }) => {
  await serve(page, notConnected);
  await page.route('**/api/projects/*/youtube/start', (route) =>
    route.fulfill({ status: 404, json: { error: 'YouTube is not set up.', code: 'not_found' } }),
  );
  await mount(<YouTubePanelHarness project={admin} />, hooks());

  await page.getByTestId(ids.youtubeConnect).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText('YouTube is not set up.');
  await expect(page.getByTestId(ids.youtubeConnect)).toBeEnabled();
});

// Layout: the panel in its tallest state (a refusal showing) at both desktop widths. Its buttons, scrolled into view, sit
// inside the window, and the page doesn't scroll sideways.
for (const viewportWidth of [1280, 1440]) {
  test(`Layout_At${viewportWidth}_ShouldKeepTheButtonsInsideTheWindow`, async ({ mount, page }) => {
    await page.setViewportSize({ width: viewportWidth, height: 720 });
    await serve(page, { ...pending, refusal });
    await mount(<YouTubePanelHarness project={admin} />, hooks());

    for (const id of [ids.youtubeDisconnect, ids.youtubeCheck]) {
      const button = page.getByTestId(id);
      await button.scrollIntoViewIfNeeded();
      const at = (await button.boundingBox())!;
      const inner = await page.evaluate(() => window.innerHeight);
      expect(at.y).toBeGreaterThanOrEqual(0);
      expect(at.y + at.height).toBeLessThanOrEqual(inner);
    }
    const refusalBox = (await page.getByTestId(ids.youtubeRefusal).boundingBox())!;
    expect(refusalBox.x + refusalBox.width).toBeLessThanOrEqual(viewportWidth);
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    );
    expect(overflow).toBeLessThanOrEqual(0);
  });
}
