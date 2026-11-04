import { expect, test } from '@playwright/experimental-ct-react';
import failed from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-failed.json' with { type: 'json' };
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { componentTestIds } from '../../components/testIds';
import { DemoPlayerHarness } from './ct/DemoPlayerHarness';
import { demosTestIds as ids } from './testIds';

const failedDemo = failed as unknown as SprintDemoDto;
const retryUrl = `**/api/sprints/${failedDemo.sprintId}/demo/youtube/retry`;

test('Retry_WhenClicked_ShouldCallAndShowThePendingAnswer', async ({ mount, page }) => {
  let calls = 0;
  const answer = {
    ...ready,
    youTube: { ...ready.youTube, state: 'pending' },
  };
  await page.route(retryUrl, (route) => {
    calls += 1;
    return route.fulfill({ json: answer });
  });
  const component = await mount(<DemoPlayerHarness demo={failedDemo} />);

  await component.getByTestId(ids.demoYoutubeRetry).click();

  await expect.poll(() => calls).toBe(1);
});

test('Retry_WhenClicked_ShouldCallAndToastOnFailure', async ({ mount, page }) => {
  await page.route(retryUrl, (route) =>
    route.fulfill({
      status: 409,
      json: { error: 'YouTube is not connected.', code: 'conflict' },
    }),
  );
  const component = await mount(<DemoPlayerHarness demo={failedDemo} />);

  await component.getByTestId(ids.demoYoutubeRetry).click();

  await expect(component.getByTestId(componentTestIds.toast)).toContainText(
    'YouTube is not connected.',
  );
  await expect(component.getByTestId(ids.demoVideo)).toBeVisible();
});

test('Retry_WhenNotAllowed_ShouldNotOfferTheButton', async ({ mount }) => {
  const denied = {
    ...failedDemo,
    actions: { ...failedDemo.actions, canRetryYouTube: false },
  };
  const component = await mount(<DemoPlayerHarness demo={denied} />);

  await expect(component.getByTestId(ids.demoYoutubeNotice)).toBeVisible();
  await expect(component.getByTestId(ids.demoYoutubeRetry)).toHaveCount(0);
});
