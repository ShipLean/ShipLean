import type { RetroPointDto } from '../../../../api/retros';
import { retroItem } from '../../ct/retroFixtures';
import { useRetroItemAddress } from '../useRetroItemAddress';

const points = retroItem.categories.flatMap((column) => column.groups.flatMap((g) => g.points));
const wentWell: RetroPointDto = points.find((p) => p.category === 'wentWell')!;
const improve: RetroPointDto = points.find((p) => p.category === 'improve')!;

/** The page's address hook with two buttons that report a point added, as the board's Add does. */
export function ItemAddedHarness() {
  const { address, onAdded } = useRetroItemAddress();
  return (
    <div>
      <output data-testid="address">{`retro=${address.retro ?? ''} decide=${address.decide}`}</output>
      <button type="button" onClick={() => onAdded({ ...improve, retroId: 'other-retro' })}>
        added to another retro
      </button>
      <button type="button" onClick={() => onAdded({ ...wentWell, retroId: address.retro ?? '' })}>
        added a went well
      </button>
    </div>
  );
}
