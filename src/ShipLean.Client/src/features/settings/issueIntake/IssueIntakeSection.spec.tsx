import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { UserRef } from '../../../api/refs';
import { settingsTestIds as ids } from '../testIds';
import { issueIntakeSample, stubIssueIntake } from './ct/issueIntakeStub';
import { IssueIntakeSection } from './IssueIntakeSection';

const project = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
const priya = issueIntakeSample.runAs as UserRef;
const tom: UserRef = {
  id: 'tom-id',
  name: 'Tom Ng',
  initials: 'TN',
  avatarColour: '#112233',
  photoUrl: null,
};
const members = [priya, tom].map((user) => ({
  user,
  email: `${user.id}@example.com`,
  role: 'member',
  status: 'active',
}));

async function stubMembers(page: import('playwright').Page) {
  await page.route('**/api/projects/*/members', (route) => route.fulfill({ json: members }));
}

test('IssueIntake_WhenNotReady_ShouldListMissingItems', async ({ mount, page }) => {
  await stubMembers(page);
  await stubIssueIntake(page, {
    ...issueIntakeSample,
    enabled: false,
    runAs: null,
    readiness: {
      hasGitHubRepository: true,
      hasAgentConnection: false,
      hasGitHubToken: true,
      hasWebhookSecret: false,
      hasRunAs: false,
    },
  });
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  const missing = page.getByTestId(ids.issueIntakeMissing);
  await expect(missing).toHaveCount(3);
  await expect(missing.nth(0)).toContainText('no agent connection');
  await expect(missing.nth(0).getByRole('link')).toHaveAttribute(
    'href',
    '/p/SHIP/settings/general',
  );
  await expect(missing.nth(1)).toContainText('webhook secret');
  await expect(missing.nth(2)).toContainText('run-as member');
  await expect(page.getByTestId(ids.issueIntakeRepository)).toContainText('ShipLean/ShipLean');
});

test('IssueIntake_WhenSaved_ShouldSendDto', async ({ mount, page }) => {
  await stubMembers(page);
  const saves = await stubIssueIntake(page, undefined, [priya, tom]);
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await expect(page.getByTestId(ids.issueIntakeInstructions)).toHaveValue(
    issueIntakeSample.defaultProposalInstructions,
  );
  await page.getByTestId(ids.issueIntakeRunAs).selectOption(tom.id);
  await page.getByTestId(ids.issueIntakeInstructions).fill('Keep it short.');
  await page.getByTestId(ids.issueIntakeMaxRounds).fill('3');
  await page.getByTestId(ids.issueIntakeSave).click();
  await expect.poll(() => saves.length).toBe(1);
  expect(saves[0]).toEqual({
    enabled: true,
    runAsUserId: tom.id,
    proposalInstructions: 'Keep it short.',
    maxRounds: 3,
  });

  await page.getByTestId(ids.issueIntakeReset).click();
  await page.getByTestId(ids.issueIntakeSave).click();
  await expect.poll(() => saves.length).toBe(2);
  expect(saves[1].proposalInstructions).toBe('');
});

test('IssueIntake_WhenDefaultTextPastedByHand_ShouldSendTheText', async ({ mount, page }) => {
  await stubMembers(page);
  const saves = await stubIssueIntake(page, undefined, [priya]);
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  const box = page.getByTestId(ids.issueIntakeInstructions);
  await expect(box).toHaveValue(issueIntakeSample.defaultProposalInstructions);
  await box.fill('x');
  await box.fill(issueIntakeSample.defaultProposalInstructions);
  await expect(page.getByTestId(ids.issueIntakeReset)).toBeEnabled();
  await page.getByTestId(ids.issueIntakeSave).click();
  await expect.poll(() => saves.length).toBe(1);
  expect(saves[0].proposalInstructions).toBe(issueIntakeSample.defaultProposalInstructions);
});

test('IssueIntake_WhenNotMember_ShouldTieErrorToRunAsAndFocusIt', async ({ mount, page }) => {
  await stubMembers(page);
  await stubIssueIntake(page, undefined, [priya]);
  await page.route('**/api/projects/*/settings/issue-intake', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({
          status: 400,
          json: { error: 'That person is not a member.', code: 'not_member', field: 'runAsUserId' },
        })
      : route.fallback(),
  );
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await page.getByTestId(ids.issueIntakeSave).click();
  const error = page.getByTestId(ids.issueIntakeError);
  await expect(error).toContainText('not a member');
  const runAs = page.getByTestId(ids.issueIntakeRunAs);
  await expect(runAs).toHaveAttribute('aria-invalid', 'true');
  const errorId = await error.getAttribute('id');
  await expect(runAs).toHaveAttribute('aria-describedby', errorId ?? 'missing');
  await expect(runAs).toBeFocused();
});

test('IssueIntake_WhenSaveFails_ShouldKeepEditsAndRunAsWhenTurnedOff', async ({ mount, page }) => {
  await stubMembers(page);
  const saves = await stubIssueIntake(page, undefined, [priya, tom]);
  let fail = true;
  await page.route('**/api/projects/*/settings/issue-intake', (route) =>
    route.request().method() === 'PUT' && fail
      ? route.fulfill({
          status: 409,
          json: { error: 'Nope.', code: 'intake_on', field: 'enabled' },
        })
      : route.fallback(),
  );
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await page.getByTestId(ids.issueIntakeRunAs).selectOption(tom.id);
  await page.getByTestId(ids.issueIntakeEnabled).uncheck();
  await page.getByTestId(ids.issueIntakeInstructions).fill('Keep it short.');
  await page.getByTestId(ids.issueIntakeSave).click();
  await expect(page.getByTestId(ids.issueIntakeError)).toBeVisible();
  await expect(page.getByTestId(ids.issueIntakeInstructions)).toHaveValue('Keep it short.');
  await expect(page.getByTestId(ids.issueIntakeRunAs)).toHaveValue(tom.id);

  fail = false;
  await page.getByTestId(ids.issueIntakeSave).click();
  await expect.poll(() => saves.length).toBe(1);
  expect(saves[0]).toMatchObject({ enabled: false, runAsUserId: tom.id });
});

test('IssueIntake_WhenMembersLoading_ShouldBlockSave', async ({ mount, page }) => {
  let release = () => {};
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/projects/*/members', async (route) => {
    await gate;
    await route.fulfill({ json: members });
  });
  await stubIssueIntake(page, undefined, [priya]);
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await expect(page.getByTestId(ids.issueIntakeMembersLoading)).toBeVisible();
  await expect(page.getByTestId(ids.issueIntakeSave)).toBeDisabled();
  release();
  await expect(page.getByTestId(ids.issueIntakeSave)).toBeEnabled();
  await expect(page.getByTestId(ids.issueIntakeMembersLoading)).toHaveCount(0);
});

test('IssueIntake_WhenMembersFail_ShouldShowErrorAndRetry', async ({ mount, page }) => {
  let ok = false;
  await page.route('**/api/projects/*/members', (route) =>
    ok ? route.fulfill({ json: members }) : route.fulfill({ status: 500, json: { error: 'boom' } }),
  );
  await stubIssueIntake(page, undefined, [priya]);
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await expect(page.getByTestId(ids.issueIntakeMembersError)).toBeVisible();
  await expect(page.getByTestId(ids.issueIntakeSave)).toBeDisabled();
  ok = true;
  await page.getByTestId(ids.issueIntakeMembersRetry).click();
  await expect(page.getByTestId(ids.issueIntakeMembersError)).toHaveCount(0);
  await expect(page.getByTestId(ids.issueIntakeSave)).toBeEnabled();
});

test('IssueIntake_WhenRepositoryTaken_ShouldShowError', async ({ mount, page }) => {
  await stubMembers(page);
  await stubIssueIntake(page, undefined, [priya]);
  await page.route('**/api/projects/*/settings/issue-intake', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({
          status: 409,
          json: {
            error: 'Another project already takes issues from ShipLean/ShipLean.',
            code: 'repository_taken',
            field: 'enabled',
          },
        })
      : route.fallback(),
  );
  await mount(<IssueIntakeSection project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await page.getByTestId(ids.issueIntakeSave).click();
  const error = page.getByTestId(ids.issueIntakeError);
  await expect(error).toContainText('Another project already takes issues');
  await expect(error).toHaveAttribute('data-field', 'enabled');
});

test('IssueIntake_WhenMember_ShouldBeReadOnly', async ({ mount, page }) => {
  await stubMembers(page);
  await stubIssueIntake(page);
  await mount(<IssueIntakeSection project={{ ...project, role: 'member' } as ProjectDto} />, {
    hooksConfig: { route: '/p/SHIP/settings/integrations' },
  });

  await expect(page.getByTestId(ids.issueIntakeEnabled)).toBeDisabled();
  await expect(page.getByTestId(ids.issueIntakeRunAs)).toBeDisabled();
  await expect(page.getByTestId(ids.issueIntakeInstructions)).toBeDisabled();
  await expect(page.getByTestId(ids.issueIntakeReset)).toBeDisabled();
  await expect(page.getByTestId(ids.issueIntakeMaxRounds)).toBeDisabled();
  await expect(page.getByTestId(ids.issueIntakeSave)).toHaveCount(0);
});
