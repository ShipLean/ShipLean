import { expect, test } from '@playwright/experimental-ct-react';
import none from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { ToastHost } from '../../components/ToastHost';
import { componentTestIds } from '../../components/testIds';
import { SprintDemoPanel } from './SprintDemoPanel';
import { demosTestIds as ids } from './testIds';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const project = { id: 'p-ship', key: 'SHIP', role: 'member' } as ProjectDto;
const sprint = { id: none.sprintId, name: 'Sprint 24' } as SprintDto;

const view = (
  <>
    <SprintDemoPanel project={project} sprint={sprint} />
    <ToastHost />
  </>
);

const stubDemo = (page: Routable, get: unknown, post?: { status: number; json: unknown }) =>
  page.route('**/api/sprints/*/demo', async (route) => {
    if (route.request().method() === 'POST' && post) return route.fulfill(post);
    return route.fulfill({ json: get });
  });

test('Panel_WhenNone_ShouldShowCreate', async ({ mount, page }) => {
  await stubDemo(page, none);
  const component = await mount(view);

  await expect(component.getByTestId(ids.demoStatusBand)).toBeVisible();
  await expect(component.getByTestId(ids.demoCreate)).toBeEnabled();
  await expect(component.getByTestId(ids.demoVideo)).toHaveCount(0);
});

test('Panel_WhenCreateFails_ShouldToast', async ({ mount, page }) => {
  await stubDemo(page, none, {
    status: 400,
    json: { error: 'The project has no repository.', code: 'validation' },
  });
  const component = await mount(view);

  await component.getByTestId(ids.demoCreate).click();

  await expect(component.getByTestId(componentTestIds.toast)).toContainText(
    'The project has no repository.',
  );
});

test('Panel_WhenReady_ShouldMountTheVideoWithContentUrl', async ({ mount, page }) => {
  await stubDemo(page, ready);
  const component = await mount(view);

  const video = component.getByTestId(ids.demoVideo);
  await expect(video).toHaveAttribute('src', ready.video.contentUrl);
  await expect(video).toHaveAttribute('controls', '');
});
