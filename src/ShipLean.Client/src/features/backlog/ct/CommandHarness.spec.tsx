import { expect, test } from '@playwright/experimental-ct-react';
import { CommandHarness } from './CommandHarness';
import { CommandHarnessProbe } from './CommandHarnessProbe';

test('CommandHarness_WhenACommandIsRunByName_ShouldReachItsHandlerWithTheArgument', async ({
  mount,
  page,
}) => {
  await mount(
    <CommandHarness>
      <CommandHarnessProbe />
    </CommandHarness>,
  );
  await expect(page.getByTestId('command-probe')).toHaveText('none');
  await page.getByTestId('run-command-focusNext').click();
  await expect(page.getByTestId('command-probe')).toHaveText('none');
  await page.getByTestId('command-arg').fill('row-1');
  await page.getByTestId('run-command-toggleSelect').click();
  await expect(page.getByTestId('command-probe')).toHaveText('ran:row-1');
});
