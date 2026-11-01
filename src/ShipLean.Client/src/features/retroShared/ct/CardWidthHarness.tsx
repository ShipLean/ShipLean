import type { RetroCardDto } from '../../../api/retros';
import { RetroCard } from '../RetroCard';

/** Mounts a RetroCard in a box of a given width: the card follows its container, not the window. */
export function CardWidthHarness({ card, width }: { card: RetroCardDto; width: number }) {
  return (
    <div style={{ width }}>
      <RetroCard card={card} />
    </div>
  );
}
