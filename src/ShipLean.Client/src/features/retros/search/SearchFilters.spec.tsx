import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { retrosTestIds as ids } from '../testIds';
import { stubRetros } from '../ct/retroFixtures';

const open = (route: string) => ({ hooksConfig: { route } });

test('SearchFilters_WhenEachControlChanges_ShouldWriteItsQueryParameter', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await expect(page.getByTestId(ids.retrosFilterSprint)).toBeVisible();
  // The router keeps the address in memory, so the filters show in what the screen asks the server.
  const asked = () => Object.fromEntries(new URLSearchParams(stub.reads('/retros').at(-1)?.query));

  await page.getByTestId(ids.retrosFilterCategory).selectOption('improve');
  await page.getByTestId(ids.retrosFilterLevel).selectOption('features');
  await page.getByTestId(ids.retrosFilterRole).selectOption('conductor');
  // click, not check(): the checkbox is controlled by the address, which updates in a router transition after the click.
  await page.getByTestId(ids.retrosFilterRecurring).click();
  await expect(page.getByTestId(ids.retrosFilterRecurring)).toBeChecked();
  await page.getByTestId(ids.retrosFilterTheme).selectOption({ index: 1 });
  await page.getByTestId(ids.retrosFilterSprint).selectOption({ index: 1 });
  await expect.poll(asked).toMatchObject({
    category: 'improve',
    level: 'features',
    role: 'conductor',
    recurring: 'true',
  });
  await expect.poll(() => asked().theme).toBeTruthy();
  await expect.poll(() => asked().sprint).toBeTruthy();
});

test('SearchFilters_WhenDecisionNoSprintAndUnthemedChange_ShouldWriteTheAddress', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await expect(page.getByTestId(ids.retrosFilterDecision)).toBeVisible();
  const asked = () => Object.fromEntries(new URLSearchParams(stub.reads('/retros').at(-1)?.query));

  await page.getByTestId(ids.retrosFilterDecision).selectOption('notNeeded');
  await expect.poll(asked).toMatchObject({ decision: 'notNeeded' });
  await page.getByTestId(ids.retrosFilterNosprint).click();
  await page.getByTestId(ids.retrosFilterUnthemed).click();
  await expect.poll(asked).toMatchObject({
    decision: 'notNeeded',
    noSprint: 'true',
    unthemed: 'true',
  });
  await page.getByTestId(ids.retrosFilterClear).click();
  await expect.poll(() => stub.reads('/retros').at(-1)?.query).toBe('');
});

test('SearchFilters_WhenTheAddressOpensWithUnthemed_ShouldShowAndAskForIt', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?unthemed=true&decision=bogus'));
  await expect(page.getByTestId(ids.retrosFilterUnthemed)).toBeChecked();
  await expect(page.getByTestId(ids.retrosFilterDecision)).toHaveValue('');
  await expect.poll(() => stub.reads('/retros').at(-1)?.query).toBe('unthemed=true');
});

test('SearchFilters_WhenTwoControlsChangeBeforeTheScreenRenders_ShouldKeepBoth', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await expect(page.getByTestId(ids.retrosFilterCategory)).toBeVisible();
  // Both changes land in one task, so the second runs before React has rendered the first.
  await page.evaluate(
    ([category, role]) => {
      for (const [id, value] of [
        [category, 'improve'],
        [role, 'conductor'],
      ]) {
        const select = document.querySelector<HTMLSelectElement>(`[data-testid="${id}"]`)!;
        const set = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!;
        set.call(select, value);
        select.dispatchEvent(new Event('change', { bubbles: true }));
      }
    },
    [ids.retrosFilterCategory, ids.retrosFilterRole],
  );
  await expect
    .poll(() => Object.fromEntries(new URLSearchParams(stub.reads('/retros').at(-1)?.query)))
    .toMatchObject({ category: 'improve', role: 'conductor' });
});

test('SearchFilters_WhenTyping_ShouldWriteQOnceTypingSettles', async ({ mount, page }) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await page.getByTestId(ids.retrosSearchBox).pressSequentially('screenshot');
  await expect
    .poll(() => stub.reads('/retros').map((call) => new URLSearchParams(call.query).get('q')))
    .toContain('screenshot');
  expect(
    stub.reads('/retros').filter((call) => new URLSearchParams(call.query).has('q')),
  ).toHaveLength(1);
});

test('SearchFilters_WhenClearClicked_ShouldResetEveryFilter', async ({ mount, page }) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(
    <AppRoutes />,
    open('/p/SHIP/retros?q=screenshot&category=improve&level=features&role=person&recurring=true'),
  );
  await expect(page.getByTestId(ids.retrosSearchBox)).toHaveValue('screenshot');
  await page.getByTestId(ids.retrosFilterClear).click();
  await expect(page.getByTestId(ids.retrosSearchBox)).toHaveValue('');
  await expect(page.getByTestId(ids.retrosFilterCategory)).toHaveValue('');
  await expect(page.getByTestId(ids.retrosFilterRecurring)).not.toBeChecked();
  await expect.poll(() => stub.reads('/retros').at(-1)?.query).toBe('');
});

for (const theme of ['light', 'dark'] as const) {
  test(`SearchFilters_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, open('/p/SHIP/retros?q=screenshot'));
    const filters = page.getByTestId(ids.retrosFilterSprint).locator('..');
    await expect(filters).toHaveScreenshot(`search-filters.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
