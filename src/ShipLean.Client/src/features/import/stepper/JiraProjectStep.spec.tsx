import { expect, test } from '@playwright/experimental-ct-react';
import { connection, project, stubImport } from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { JiraProjectStep } from './JiraProjectStep';

test('JiraProjectStep_WhenARadioIsChosenByKeyboard_ShouldPickThatProject', async ({
  mount,
  page,
}) => {
  await stubImport(page, connection);
  const picked: string[] = [];
  await mount(
    <JiraProjectStep projectId={project.id} selected={null} onPick={(key) => picked.push(key)} />,
  );
  const radios = page.getByTestId(ids.projectRadio);
  await expect(radios).toHaveCount(2);
  await radios.first().focus();
  await page.keyboard.press('Space');
  await expect.poll(() => picked).toEqual(['PLATFORM']);
});
