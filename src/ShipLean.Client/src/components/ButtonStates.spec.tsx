import { expect, test } from '@playwright/experimental-ct-react';
import { ButtonStatesHarness, type ButtonClass } from './ct/ButtonStatesHarness';
import { buttonHarnessIds as ids } from './ct/buttonHarnessIds';

// The shared button classes when disabled (K3, RETRO-1): faded with a not-allowed cursor, light and dark. The shot
// puts the enabled button beside the disabled one, so a disabled button that looks enabled shows as a pair of twins.

const THEMES = ['light', 'dark'] as const;

const CASES: { name: string; variant: ButtonClass; shot: string }[] = [
  { name: 'ButtonSignal', variant: 'btn-signal', shot: 'button-signal' },
  { name: 'ButtonGhost', variant: 'btn-ghost', shot: 'button-ghost' },
  { name: 'ButtonDangerGhost', variant: 'btn-danger-ghost', shot: 'button-danger-ghost' },
];

for (const { name, variant, shot } of CASES) {
  for (const theme of THEMES) {
    test(`${name}_WhenDisabled_ShouldLookDisabled_${theme}`, async ({ mount, page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await mount(<ButtonStatesHarness variant={variant} />);

      await expect(page.getByTestId(ids.ctButtonEnabled)).toHaveCSS('opacity', '1');
      const disabled = page.getByTestId(ids.ctButtonDisabled);
      await expect(disabled).toBeDisabled();
      await expect(disabled).toHaveCSS('opacity', '0.5');
      await expect(disabled).toHaveCSS('cursor', 'not-allowed');
      await expect(page.getByTestId(ids.ctButtonStates)).toHaveScreenshot(`${shot}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}
