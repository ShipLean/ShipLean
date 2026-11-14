import { retrosTestIds as ids } from '../../testIds';
import { Tip } from './Tip';
import { unthemedWording } from './tipWording';

/** The Unthemed row's label with its tooltip. */
export function UnthemedTip({ children = 'Unthemed' }: { children?: string }) {
  return (
    <Tip data-testid={ids.retroTipUnthemed} entry="unthemed" tip={unthemedWording}>
      {children}
    </Tip>
  );
}
