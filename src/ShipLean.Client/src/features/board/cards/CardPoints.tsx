import type { CardDto } from '../../../api/views';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';
import { pointsText } from './cardStyle';

// The points pill. Stories and bugs show their points, a warn ? when unestimated. Features and Epics show their own
// points with the stories and bugs below greyed beside them (20 · Σ26), warn when the sum is over by more than 25 %.
export function CardPoints({ card, level }: { card: CardDto; level: Level }) {
  if (level === 'stories') {
    const unestimated = card.points === null;
    return (
      <span
        data-testid={ids.cardPoints}
        data-unestimated={unestimated}
        className={`ml-auto inline-flex h-[18px] min-w-5 items-center justify-center rounded-full px-[5px] text-[length:var(--text-xs)] font-semibold tabular-nums ${
          unestimated ? 'text-caution' : 'bg-sunken'
        }`}
        style={
          unestimated
            ? { background: 'color-mix(in srgb, var(--color-warn) 16%, transparent)' }
            : undefined
        }
      >
        {pointsText(card.points)}
      </span>
    );
  }
  if (card.points === null && card.childPoints === null) return null;
  return (
    <span className="ml-auto inline-flex items-center gap-1 text-[length:var(--text-xs)] tabular-nums">
      <span
        data-testid={ids.cardPoints}
        className="inline-flex h-[18px] min-w-5 items-center justify-center rounded-full bg-sunken px-[5px] font-semibold"
      >
        {pointsText(card.points)}
      </span>
      {card.childPoints === null ? null : (
        <span
          data-testid={ids.cardChildPoints}
          data-warn={card.sumWarn}
          className={card.sumWarn ? 'font-semibold text-caution' : 'text-ink-muted'}
        >
          Σ{card.childPoints}
        </span>
      )}
    </span>
  );
}
