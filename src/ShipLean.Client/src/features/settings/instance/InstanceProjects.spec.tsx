import { expect, test } from '@playwright/experimental-ct-react';
import type { InstanceProjectDto } from '../../../api/projects';
import { settingsTestIds as ids } from '../testIds';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { InstanceProjects } from './InstanceProjects';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const priya = {
  id: 'u-priya',
  name: 'Priya Shah',
  initials: 'PS',
  avatarColour: '#2f7fd8',
  photoUrl: null,
};

const project = (key: string, over: Partial<InstanceProjectDto> = {}): InstanceProjectDto => ({
  id: `p-${key}`,
  key,
  name: `${key} project`,
  memberCount: 3,
  admins: [priya],
  hasActiveAdmin: true,
  itemCount: 1200,
  yourRole: null,
  ...over,
});

const initial = (): InstanceProjectDto[] => [
  project('SHIP', { yourRole: 'admin' }),
  project('PAY', { yourRole: 'member' }),
  project('ARCH', { admins: [], hasActiveAdmin: false }),
];

const stub = async (page: Routable, status?: number) => {
  const joins: string[] = [];
  const state = initial();
  await page.route('**/api/instance/projects**', async (route) => {
    const request = route.request();
    if (status) return route.fulfill({ status, json: { error: 'No.', code: 'forbidden' } });
    if (request.method() === 'POST') {
      const path = new URL(request.url()).pathname;
      joins.push(path);
      const target = state.find((p) => path.includes(p.id));
      if (target) {
        target.yourRole = 'admin';
        target.hasActiveAdmin = true;
        target.admins = [priya];
      }
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ json: state });
  });
  await page.route('**/api/admin/users', (route) =>
    route.fulfill({ json: { users: [], administeredProjects: [] } }),
  );
  await page.route('**/api/projects', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/me', (route) => route.fulfill({ json: { id: 'me' } }));
  return joins;
};

test('Projects_WhenLoaded_ShouldListEveryProjectWithYourRole', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceProjects />);

  await expect(view.getByTestId(ids.projectsRow)).toHaveCount(3);
  const rows = view.getByTestId(ids.projectsRow);
  await expect(rows.nth(0).getByTestId(ids.projectsYou)).toHaveText('Admin');
  await expect(rows.nth(0).getByTestId(ids.projectsSettings)).toHaveAttribute(
    'href',
    '/p/SHIP/settings/general',
  );
  await expect(rows.nth(1).getByTestId(ids.projectsYou)).toHaveText('Member');
  await expect(rows.nth(1).getByTestId(ids.projectsJoin)).toBeVisible();
  await expect(view.getByTestId(ids.projectsCount)).toContainText('3 on this install');
});

test('Projects_WhenNoActiveAdmin_ShouldFlagItAndOfferJoin', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceProjects />);

  await expect(view.getByTestId(ids.projectsBanner)).toHaveCount(1);
  await expect(view.getByTestId(ids.projectsBanner)).toContainText(
    'ARCH · ARCH project has no active admin',
  );
  const flagged = view.getByTestId(ids.projectsRow).filter({ hasText: 'ARCH' });
  await expect(flagged.getByTestId(ids.projectsNoAdmin)).toBeVisible();
  await expect(
    view.getByTestId(ids.projectsRow).first().getByTestId(ids.projectsNoAdmin),
  ).toHaveCount(0);
});

test('Projects_WhenJoinedFromBanner_ShouldPostOnceAndClearTheFlag', async ({ mount, page }) => {
  const joins = await stub(page);
  const view = await mount(<InstanceProjects />);

  await view.getByTestId(ids.projectsBannerJoin).click();

  await expect(view.getByTestId(ids.projectsBanner)).toHaveCount(0);
  await expect(
    view.getByTestId(ids.projectsRow).filter({ hasText: 'ARCH' }).getByTestId(ids.projectsYou),
  ).toHaveText('Admin');
  expect(joins).toEqual(['/api/instance/projects/p-ARCH/join']);
});

test('Projects_WhenNotInstanceAdmin_ShouldShowDenied', async ({ mount, page }) => {
  await stub(page, 403);
  const view = await mount(<InstanceProjects />);

  await expect(page.getByTestId(ids.instanceDenied)).toBeVisible();
  await expect(view.getByTestId(ids.projectsTable)).toHaveCount(0);
});

test('Projects_WhenLoaded_ShouldOfferNewProjectInTheHeader', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceProjects />);

  await expect(view.getByTestId(ids.projectsNew)).toHaveAttribute('href', '/projects/new');
});

test('Projects_WhenTwoJoinsAreInFlight_ShouldToastBothAndLockOnlyTheirOwnButtons', async ({
  mount,
  page,
}) => {
  const releases: (() => void)[] = [];
  const state = initial();
  state[0] = project('SHIP', { yourRole: 'member' });
  await page.route('**/api/instance/projects**', async (route) => {
    if (route.request().method() === 'POST') {
      await new Promise<void>((resolve) => releases.push(resolve));
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ json: state });
  });
  await page.route('**/api/admin/users', (route) =>
    route.fulfill({ json: { users: [], administeredProjects: [] } }),
  );
  await page.route('**/api/projects', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/me', (route) => route.fulfill({ json: { id: 'me' } }));
  const view = await mount(
    <>
      <InstanceProjects />
      <ToastHost />
    </>,
  );
  const joinOf = (key: string) =>
    view.getByTestId(ids.projectsRow).filter({ hasText: key }).getByTestId(ids.projectsJoin);

  await joinOf('SHIP').click();
  await joinOf('PAY').click();

  await expect(joinOf('SHIP')).toBeDisabled();
  await expect(joinOf('PAY')).toBeDisabled();
  await expect.poll(() => releases.length).toBe(2);
  releases.forEach((release) => release());
  const toasts = view.getByTestId(componentTestIds.toast);
  await expect(toasts).toHaveCount(2);
  await expect(toasts.filter({ hasText: 'SHIP' })).toHaveCount(1);
  await expect(toasts.filter({ hasText: 'PAY' })).toHaveCount(1);
});
