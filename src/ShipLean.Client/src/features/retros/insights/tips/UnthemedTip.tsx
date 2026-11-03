import { retrosTestIds as ids } from '../../testIds';
import { Tip } from './Tip';

const TEXT =
  'Went badly and Not in the plan points with no theme. Give them one in Search, or run Decide what to do, which assigns themes as it goes.';

/** The Unthemed row's label with its tooltip. */
export function UnthemedTip({ children = 'Unthemed' }: { children?: string }) {
  return (
    <Tip data-testid={ids.retroTipUnthemed} tip={TEXT}>
      {children}
    </Tip>
  );
}
