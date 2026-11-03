import { retrosTestIds as ids } from '../../testIds';
import { Tip } from './Tip';

const TEXT =
  'Points on retros with no sprint. They count in Total, but not towards Recurring or a verdict, because they have no time order. Week mode places them by the day they were logged.';

/** The No sprint heading's label with its tooltip; the heading cell itself is T2's. */
export function NoSprintTip({ children = 'No sprint' }: { children?: string }) {
  return (
    <Tip data-testid={ids.retroTipNosprint} tip={TEXT}>
      {children}
    </Tip>
  );
}
