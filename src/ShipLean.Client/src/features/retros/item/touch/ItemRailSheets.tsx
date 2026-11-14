import type { RetroItemDto } from '@shiplean/core/api/retros';
import { Sheet } from '../../../../touch/Sheet';
import { RailActions, RailCompiled, RailWriters } from '../ItemRail';

export type RailSheet = 'actions' | 'compiled' | 'writers';

/** The rail's three sections as K5 sheets at touch (R5): only the open one renders. */
export function ItemRailSheets({
  projectKey,
  item,
  open,
  onClose,
}: {
  projectKey: string;
  item: RetroItemDto;
  open: RailSheet | null;
  onClose: () => void;
}) {
  return (
    <>
      <Sheet open={open === 'actions'} title="Action items" onClose={onClose}>
        <p className="m-0 pb-1 text-[length:var(--text-xs)] text-ink-muted">
          Raised from these points. Each starts in New with no sprint; pulling it into a sprint is
          the approval.
        </p>
        <RailActions item={item} sheet />
      </Sheet>
      <Sheet open={open === 'compiled'} title="Compiled from" onClose={onClose}>
        <RailCompiled projectKey={projectKey} item={item} sheet />
      </Sheet>
      <Sheet open={open === 'writers'} title="Written by" onClose={onClose}>
        <RailWriters item={item} sheet />
      </Sheet>
    </>
  );
}
