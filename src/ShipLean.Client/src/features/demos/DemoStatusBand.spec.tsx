import { expect, test } from '@playwright/experimental-ct-react';
import none from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import working from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-working.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { componentTestIds } from '../../components/testIds';
import { DemoBandHarness } from './ct/DemoBandHarness';
import { demosTestIds as ids } from './testIds';

const base = none as unknown as SprintDemoDto;
const withActions = (demo: SprintDemoDto, actions: Partial<SprintDemoDto['actions']>) => ({
  ...demo,
  actions: { ...demo.actions, ...actions },
});

const closable: SprintDemoDto = {
  ...withActions(base, { canCreate: false }),
  status: 'closable',
  statusMessage: 'Close the sprint to make its demo.',
};
const blocked: SprintDemoDto = {
  ...withActions(base, {
    canCreate: false,
    createBlockedMessage: 'Connect an agent to this project, then create the demo here.',
  }),
  status: 'blocked',
  statusMessage: "The automatic demo didn't start: Storefront has no agent connection.",
};
const failed: SprintDemoDto = {
  ...(working as unknown as SprintDemoDto),
  status: 'failed',
  statusMessage: 'The upload link expired before a video came back.',
  run: { ...(working as unknown as SprintDemoDto).run!, sessionState: 'ended' },
  actions: { canCreate: true, createBlockedMessage: null, canRetryYouTube: false },
};
const noAgent: SprintDemoDto = withActions(base, {
  canCreate: false,
  createBlockedMessage: 'The project has no agent connection.',
});
const readyDemo = ready as unknown as SprintDemoDto;
const workingDemo = working as unknown as SprintDemoDto;

test('Band_WhenClosable_ShouldSayCloseItAndHideCreate', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={closable} />);

  await expect(component.getByTestId(ids.demoStatusMessage)).toHaveText(
    'Close the sprint to make its demo.',
  );
  await expect(component.getByTestId(ids.demoCreate)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoCreateBlocked)).toHaveCount(0);
});

test('Band_WhenNone_ShouldOfferCreate', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={base} />);

  await expect(component.getByTestId(ids.demoStatusMessage)).toHaveText(
    'Make a demo of Sprint 24.',
  );
  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
  await expect(component.getByTestId(ids.demoCreate)).toHaveText('Create demo');
});

test('Band_WhenBlocked_ShouldDisableCreateAndSayWhy', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={blocked} />);

  await expect(component.getByTestId(ids.demoStatusMessage)).toHaveText(blocked.statusMessage!);
  await expect(component.getByTestId(ids.demoStatusBand)).toContainText(
    blocked.actions.createBlockedMessage!,
  );
  await expect(component.getByTestId(ids.demoCreateBlocked)).toBeDisabled();
  await expect(component.getByTestId(ids.demoCreate)).toHaveCount(0);
});

test('Band_WhenCreateBlockedByNoAgent_ShouldNameWhatIsMissing', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={noAgent} />);

  await expect(component.getByTestId(ids.demoStatusBand)).toContainText(
    'The project has no agent connection.',
  );
  await expect(component.getByTestId(ids.demoCreateBlocked)).toBeDisabled();
  await expect(component.getByTestId(ids.demoCreate)).toHaveCount(0);
});

test('Band_WhenWorking_ShouldLinkTheItemAndShowSessionState', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={workingDemo} />);

  const link = component.getByTestId(ids.demoItemLink);
  await expect(link).toHaveText(workingDemo.run!.itemKey);
  await expect(link).toHaveAttribute('href', workingDemo.run!.itemUrl);
  await expect(component.getByTestId(ids.demoStatusBand)).toContainText('Agent session: working');
  await expect(component.getByTestId(ids.demoStatusBand)).toContainText(
    workingDemo.run!.uploadExpiresLabel!,
  );
  await expect(component.getByTestId(ids.demoCreate)).toHaveCount(0);
  await expect(component.getByTestId(ids.demoCreateBlocked)).toHaveCount(0);
});

test('Band_WhenFailed_ShouldSayWhyAndOfferCreate', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={failed} />);

  await expect(component.getByTestId(ids.demoStatusMessage)).toHaveText(failed.statusMessage!);
  await expect(component.getByTestId(ids.demoCreate)).toHaveText('Create demo again');
  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
  await expect(component.getByTestId(ids.demoItemLink)).toBeVisible();
});

test('Band_WhenReady_ShouldOfferRemake', async ({ mount }) => {
  const component = await mount(<DemoBandHarness demo={readyDemo} />);

  await expect(component.getByTestId(ids.demoStatusMessage)).toHaveText(
    'The demo for Sprint 24 is ready.',
  );
  await expect(component.getByTestId(ids.demoStatusBand)).toContainText(
    readyDemo.video!.deliveredLabel,
  );
  await expect(component.getByTestId(ids.demoCreate)).toHaveText('Remake demo');
});

test('Create_WhenClicked_ShouldCallOnceAndDisableWhilePending', async ({ mount, page }) => {
  let posts = 0;
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/sprints/*/demo', async (route) => {
    posts += 1;
    await gate;
    await route.fulfill({ json: workingDemo });
  });
  const component = await mount(<DemoBandHarness demo={base} />);
  const create = component.getByTestId(ids.demoCreate);

  await create.click();
  await expect(create).toBeDisabled();
  await create.click({ force: true });
  release();

  await expect.poll(() => posts).toBe(1);
});

test('Create_WhenConflict_ShouldToastTheMessage', async ({ mount, page }) => {
  await page.route('**/api/sprints/*/demo', (route) =>
    route.fulfill({
      status: 409,
      json: { error: 'An agent session is already making this demo.', code: 'conflict' },
    }),
  );
  const component = await mount(<DemoBandHarness demo={base} />);

  await component.getByTestId(ids.demoCreate).click();

  await expect(component.getByTestId(componentTestIds.toast)).toContainText(
    'An agent session is already making this demo.',
  );
  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
});

const bands = {
  closable,
  none: base,
  blocked,
  working: workingDemo,
  failed,
  ready: readyDemo,
};

for (const [name, demo] of Object.entries(bands)) {
  for (const theme of ['light', 'dark'] as const) {
    test(`Band_WhenRendered_ShouldMatchItsScreenshot_${name}_${theme}`, async ({ mount, page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const component = await mount(<DemoBandHarness demo={demo} />);
      await expect(component.getByTestId(ids.demoStatusBand)).toBeVisible();
      await expect(component).toHaveScreenshot(`demo-band-${name}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}

for (const width of [1280, 1440]) {
  test(`Band_WhenFailureMessageIsLong_ShouldNotOverflowAt${width}`, async ({ mount, page }) => {
    await page.setViewportSize({ width, height: 800 });
    const long = {
      ...failed,
      statusMessage: `The agent stopped: ${'unbroken'.repeat(40)} ${'and then some words '.repeat(20)}`,
    };
    const component = await mount(<DemoBandHarness demo={long} />);

    const band = component.getByTestId(ids.demoStatusBand);
    await expect(band).toBeVisible();
    const sizes = await band.evaluate((el) => ({
      scroll: el.scrollWidth,
      client: el.clientWidth,
      page: document.documentElement.scrollWidth,
      view: window.innerWidth,
    }));
    expect(sizes.scroll).toBeLessThanOrEqual(sizes.client);
    expect(sizes.page).toBeLessThanOrEqual(sizes.view);
    const create = await component.getByTestId(ids.demoCreate).boundingBox();
    expect(create!.x + create!.width).toBeLessThanOrEqual(width);
  });
}
