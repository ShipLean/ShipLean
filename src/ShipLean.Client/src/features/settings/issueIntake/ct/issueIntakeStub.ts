// Stubs for the component tests of the issue intake settings (page.route answers; nothing here is production code).
import type { Page } from 'playwright';
import sample from '../../../../../../ShipLean.TestSupport/DtoFixtures/issueIntake/issue-intake-settings.json' with { type: 'json' };
import type { IssueIntakeSaveDto, IssueIntakeSettingsDto } from '../types';

export const issueIntakeSample: IssueIntakeSettingsDto = sample;

/** Answers GET with `settings` and PUT with the saved fields over it; returns the PUT bodies the page sent. */
export async function stubIssueIntake(page: Page, settings: IssueIntakeSettingsDto = issueIntakeSample) {
  const saves: IssueIntakeSaveDto[] = [];
  await page.route('**/api/projects/*/settings/issue-intake', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.fulfill({ json: settings });
      return;
    }

    const save = route.request().postDataJSON() as IssueIntakeSaveDto;
    saves.push(save);
    await route.fulfill({
      json: {
        ...settings,
        enabled: save.enabled,
        proposalInstructions: save.proposalInstructions,
        maxRounds: save.maxRounds,
      },
    });
  });
  return saves;
}
