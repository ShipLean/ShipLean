import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { useItemPeek } from '../../../itemPeek/useItemPeek';
import { ThemesAddressHarness } from './ThemesAddressHarness';

/** The address harness with a button that closes the open item, since a component test has no peek close control to tap. */
export function ThemesItemPeekHarness({ insights }: { insights: RetroInsightsDto }) {
  const { close } = useItemPeek();
  return (
    <>
      <button type="button" data-testid="test-close-item" onClick={close}>
        close item
      </button>
      <ThemesAddressHarness insights={insights} />
    </>
  );
}
