import { expect, test } from '@playwright/experimental-ct-react';
import type { KeyCheckDto, ProjectCreate } from '../../../api/projects';
import { settingsTestIds as ids } from '../testIds';
import { NewProject } from './NewProject';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const me = (canCreateProjects: boolean) => ({
  id: 'u-me',
  name: 'Joel',
  email: 'joel@example.test',
  canCreateProjects,
});

const taken: Record<string, KeyCheckDto> = {
  PAY: { key: 'PAY', available: false, reason: 'taken', alternatives: ['PAYM', 'PA'] },
  CORE: { key: 'CORE', available: false, reason: 'takenByAnotherProject', alternatives: [] },
};

interface StubOptions {
  canCreate?: boolean;
  /** Key checks for these keys wait for `release()` before answering. */
  hold?: string[];
  /** The first Create is refused with a 409 and, from then on, this key is taken. */
  conflictOnFirstCreate?: boolean;
}

const stub = async (
  page: Routable,
  { canCreate = true, hold = [], conflictOnFirstCreate = false }: StubOptions = {},
) => {
  const created: ProjectCreate[] = [];
  const checked: string[] = [];
  const answers: Record<string, KeyCheckDto> = { ...taken };
  let release: () => void = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/me', (route) => route.fulfill({ json: me(canCreate) }));
  await page.route('**/api/projects', async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      const body = request.postDataJSON() as ProjectCreate;
      created.push(body);
      if (conflictOnFirstCreate && created.length === 1) {
        answers[body.key] = {
          key: body.key,
          available: false,
          reason: 'takenByAnotherProject',
          alternatives: [`${body.key}A`],
        };
        return route.fulfill({
          status: 409,
          json: { code: 'conflict', error: `The key ${body.key} was just taken.` },
        });
      }
      return route.fulfill({ json: { id: 'p-new', key: 'CSH' } });
    }
    return route.fulfill({
      json: [{ id: 'p-ship', key: 'PAY', name: 'Payments', role: 'admin', memberCount: 2 }],
    });
  });
  await page.route('**/api/projects/key-check**', async (route) => {
    const key = new URL(route.request().url()).searchParams.get('key') ?? '';
    checked.push(key);
    if (hold.includes(key)) await released;
    return route.fulfill({
      json: answers[key] ?? { key, available: true, reason: null, alternatives: [] },
    });
  });
  await page.route('**/api/projects/first-sprint**', (route) =>
    route.fulfill({
      json: { name: 'Sprint 1', startDate: '2026-10-26', endDate: '2026-11-08', goal: '' },
    }),
  );
  await page.route('**/api/users/by-email**', async (route) => {
    const email = new URL(route.request().url()).searchParams.get('email') ?? '';
    if (email !== 'mei@example.test')
      return route.fulfill({
        status: 404,
        json: { code: 'not_found', error: `No user has the email ${email}.` },
      });
    return route.fulfill({
      json: {
        id: 'u-mei',
        name: 'Mei Tanaka',
        initials: 'MT',
        avatarColour: '#112233',
        photoUrl: null,
      },
    });
  });
  return { created, checked, release };
};

test('NewProject_WhenNameTyped_ShouldSuggestKeyAndCheckIt', async ({ mount, page }) => {
  const { checked } = await stub(page);
  const view = await mount(<NewProject />);

  const request = page.waitForRequest('**/api/projects/key-check?key=CSH**');
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await request;

  await expect(view.getByTestId(ids.newProjectKey)).toHaveValue('CSH');
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create CSH');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();
  expect(checked).not.toContain('C');
});

test('NewProject_WhenKeyTypedLetterByLetter_ShouldAskOnlyOnceTypingPauses', async ({
  mount,
  page,
}) => {
  const { checked } = await stub(page);
  const view = await mount(<NewProject />);

  const request = page.waitForRequest('**/api/projects/key-check?key=CSH**');
  await view.getByTestId(ids.newProjectKey).pressSequentially('CSH', { delay: 20 });
  await request;

  await expect.poll(() => checked).toEqual(['CSH']);
});

test('NewProject_WhenKeyTaken_ShouldOfferAlternatives', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');

  await view.getByTestId(ids.newProjectKey).fill('PAY');

  await expect(view.getByTestId(ids.newProjectCheckFree)).toContainText('PAY is used by Payments');
  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'false');
  await expect(view.getByTestId(ids.newProjectAlternative)).toHaveText(['PAYM', 'PA']);
  await view.getByTestId(ids.newProjectAlternative).first().click();
  await expect(view.getByTestId(ids.newProjectKey)).toHaveValue('PAYM');
  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'true');
});

test('NewProject_WhenKeyBelongsToAnotherProject_ShouldNotSayWhoseIt', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectKey).fill('CORE');

  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveText(
    '✕ CORE is used by another project',
  );
});

test('NewProject_WhenKeyHasDigits_ShouldFlagLettersWithoutAsking', async ({ mount, page }) => {
  await page.clock.install();
  const { checked } = await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectKey).fill('A1');
  await expect(view.getByTestId(ids.newProjectCheckLetters)).toHaveAttribute('data-ok', 'false');
  await expect(view.getByTestId(ids.newProjectCheckLength)).toHaveAttribute('data-ok', 'true');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeDisabled();
  await page.clock.runFor(500);
  const request = page.waitForRequest('**/api/projects/key-check?key=ABC**');
  await view.getByTestId(ids.newProjectKey).fill('ABC');
  await page.clock.runFor(500);
  await request;

  await expect.poll(() => checked).toEqual(['ABC']);
});

test('NewProject_WhenNewKeyTyped_ShouldNotTreatTheOldAnswerAsItsOwn', async ({ mount, page }) => {
  await stub(page, { hold: ['ZZZ'] });
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create CSH');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();

  const request = page.waitForRequest('**/api/projects/key-check?key=ZZZ**');
  await view.getByTestId(ids.newProjectKey).fill('ZZZ');
  expect(await view.getByTestId(ids.newProjectCreate).isDisabled()).toBe(true);
  await request;

  await expect(view.getByTestId(ids.newProjectCreate)).toBeDisabled();
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create project');
});

test('NewProject_WhileKeyCheckPending_ShouldShowCheckingNotAFailure', async ({ mount, page }) => {
  const { release } = await stub(page, { hold: ['ZZZ'] });
  const view = await mount(<NewProject />);

  const request = page.waitForRequest('**/api/projects/key-check?key=ZZZ**');
  await view.getByTestId(ids.newProjectKey).fill('ZZZ');
  await request;

  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveText('… Checking…');
  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'pending');
  release();
  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'true');
});

test('NewProject_WhenSuggestedKeyTaken_ShouldTakeTheFirstAlternative', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectName).fill('Pay');

  await expect(view.getByTestId(ids.newProjectKey)).toHaveValue('PAYM');
  await expect(view.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'true');
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create PAYM');
});

test('NewProject_WhenCreateRefusedWithConflict_ShouldAskAgainAndShowTheServersMessage', async ({
  mount,
  page,
}) => {
  const { created, checked } = await stub(page, { conflictOnFirstCreate: true });
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create CSH');
  await view.getByTestId(ids.newProjectCreate).click();

  await expect(view.getByTestId(ids.newProjectError)).toHaveText('The key CSH was just taken.');
  await expect.poll(() => checked.filter((key) => key === 'CSH').length).toBe(2);
  await expect(view.getByTestId(ids.newProjectKey)).toHaveValue('CSHA');
  await expect(view.getByTestId(ids.newProjectCreate)).toHaveText('Create CSHA');
  expect(created).toHaveLength(1);
});

test('NewProject_WhenFilledIn_ShouldCreateWithEverythingTheFormHolds', async ({ mount, page }) => {
  const { created } = await stub(page);
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill(' Customer Support Hub ');
  await view.getByTestId(ids.newProjectFromCopy).check();
  await view.getByTestId(ids.newProjectMemberEmail).fill('mei@example.test');
  await view.getByTestId(ids.newProjectMemberAdd).click();
  await expect(view.getByTestId(ids.newProjectMemberRow)).toContainText('Mei Tanaka');
  await view.getByTestId(ids.workingDay5).click();
  await view.getByTestId(ids.newProjectSprintStart).fill('2026-10-26');
  await view.getByTestId(ids.newProjectSprintEnd).fill('2026-11-06');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();

  await view.getByTestId(ids.newProjectCreate).click();

  await expect.poll(() => created.length).toBe(1);
  expect(created[0]).toEqual({
    name: 'Customer Support Hub',
    key: 'CSH',
    copyFromProjectId: 'p-ship',
    memberEmails: ['mei@example.test'],
    workingDays: [1, 2, 3, 4],
    firstSprint: { name: 'Sprint 1', startDate: '2026-10-26', endDate: '2026-11-06', goal: '' },
  });
});

test('NewProject_WhenSprintOff_ShouldSendNoSprintAndNeedNoDates', async ({ mount, page }) => {
  const { created } = await stub(page);
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');

  await view.getByTestId(ids.newProjectSprintToggle).uncheck();
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();
  await view.getByTestId(ids.newProjectCreate).click();

  await expect.poll(() => created.length).toBe(1);
  expect(created[0].firstSprint).toBeNull();
});

test('NewProject_WhenLastWorkingDayRemoved_ShouldBlockCreate', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await view.getByTestId(ids.newProjectSprintToggle).uncheck();

  for (const day of [
    ids.workingDay1,
    ids.workingDay2,
    ids.workingDay3,
    ids.workingDay4,
    ids.workingDay5,
  ])
    await view.getByTestId(day).click();

  await expect(view.getByTestId(ids.newProjectHint)).toHaveText('Pick at least one working day');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeDisabled();
});

test('NewProject_WhenMemberEmailPartial_ShouldSayNotFound', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectMemberEmail).fill('mei');
  await view.getByTestId(ids.newProjectMemberAdd).click();

  await expect(view.getByTestId(ids.newProjectMemberError)).toContainText(
    'No user has the email mei',
  );
  await expect(view.getByTestId(ids.newProjectMemberRow)).toHaveCount(0);
});

test('NewProject_WhenOwnEmailAdded_ShouldRefuse', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectMemberEmail).fill('JOEL@example.test');
  await view.getByTestId(ids.newProjectMemberAdd).click();

  await expect(view.getByTestId(ids.newProjectMemberError)).toContainText('already on the project');
});

test('NewProject_WhenCreationLimited_ShouldShowBlockedPage', async ({ mount, page }) => {
  await stub(page, { canCreate: false });
  await mount(<NewProject />);

  await expect(page.getByTestId(ids.newProjectBlocked)).toContainText(
    'Only instance admins can create projects here',
  );
  await expect(page.getByTestId(ids.newProjectForm)).toHaveCount(0);
});

test('NewProject_WhenOnlyNameTyped_ShouldCreateWithTheServersSprintOne', async ({
  mount,
  page,
}) => {
  const { created } = await stub(page);
  const view = await mount(<NewProject />);

  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await expect(view.getByTestId(ids.newProjectSprintStart)).toHaveValue('2026-10-26');
  await expect(view.getByTestId(ids.newProjectSprintEnd)).toHaveValue('2026-11-08');
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();
  await view.getByTestId(ids.newProjectCreate).click();

  await expect.poll(() => created.length).toBe(1);
  expect(created[0].firstSprint).toEqual({
    name: 'Sprint 1',
    startDate: '2026-10-26',
    endDate: '2026-11-08',
    goal: '',
  });
});

test('NewProject_WhenOneDateEdited_ShouldKeepTheOtherAsShown', async ({ mount, page }) => {
  const { created } = await stub(page);
  const view = await mount(<NewProject />);
  await view.getByTestId(ids.newProjectName).fill('Customer Support Hub');
  await expect(view.getByTestId(ids.newProjectSprintEnd)).toHaveValue('2026-11-08');

  await view.getByTestId(ids.newProjectSprintStart).fill('2026-10-27');
  await view.getByTestId(ids.workingDay1).click();
  await expect(view.getByTestId(ids.newProjectCreate)).toBeEnabled();
  await view.getByTestId(ids.newProjectCreate).click();

  await expect.poll(() => created.length).toBe(1);
  expect(created[0].firstSprint).toMatchObject({ startDate: '2026-10-27', endDate: '2026-11-08' });
});
