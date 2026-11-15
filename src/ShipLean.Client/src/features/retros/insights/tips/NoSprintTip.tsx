import { retrosTestIds as ids } from '../../testIds';
import { Tip } from './Tip';
import { noSprintWording } from './tipWording';

/** The No sprint heading's label with its tooltip; the heading cell itself is T2's. */
export function NoSprintTip({ children = 'No sprint' }: { children?: string }) {
  return (
    <Tip data-testid={ids.retroTipNosprint} entry="nosprint" tip={noSprintWording}>
      {children}
    </Tip>
  );
}
