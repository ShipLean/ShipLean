import type { RetroPointDto } from '../../../api/retros';
import { PointRow } from '../PointRow';

/** Mounts PointRow with a render-prop `actions`: a spec can't pass one that returns JSX, because it runs in Node. */
export function PointRowActionsHarness({ point }: { point: RetroPointDto }) {
  return <PointRow point={point} actions={() => <button type="button">Archive point</button>} />;
}
