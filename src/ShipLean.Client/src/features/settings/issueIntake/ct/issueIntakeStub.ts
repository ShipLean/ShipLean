// Stubs for the component tests of the issue intake settings (page.route answers; nothing here is production code).
import type { Page } from 'playwright';
import type { UserRef } from '@shiplean/core/api/refs';
import sample from '../../../../../../ShipLean.TestSupport/DtoFixtures/issueIntake/issue-intake-settings.json' with { type: 'json' };
import type { IssueIntakeSaveDto, IssueIntakeSettingsDto } from '../types';

export const issueIntakeSample: IssueIntakeSettingsDto = sample;

/** The stored settings after a save: the submitted run-as id is resolved against `members` (null clears it, and readiness follows). */
export function applyIssueIntakeSave(
  settings: IssueIntakeSettingsDto,
  save: IssueIntakeSaveDto,
  members: UserRef[],
): IssueIntakeSettingsDto {
  const runAs = members.find((m) => m.id === save.runAsUserId) ?? null;
  return {
    ...settings,
    enabled: save.enabled,
    runAs,
    proposalInstructions: save.proposalInstructions,
    maxRounds: save.maxRounds,
    readiness: { ...settings.readiness, hasRunAs: runAs !== null },
  };
}

/** Answers GET with `settings` and PUT with the saved fields over it; returns the PUT bodies the page sent. */
export async function stubIssueIntake(
  page: Page,
  settings: IssueIntakeSettingsDto = issueIntakeSample,
  members: UserRef[] = [],
) {
  let stored = settings;
  const saves: IssueIntakeSaveDto[] = [];
  await page.route('**/api/projects/*/settings/issue-intake', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.fulfill({ json: stored });
      return;
    }

    const save = route.request().postDataJSON() as IssueIntakeSaveDto;
    saves.push(save);
    stored = applyIssueIntakeSave(stored, save, members);
    await route.fulfill({ json: stored });
  });
  return saves;
}
