import type { BoardViewDto } from '@shiplean/core/api/views';
import { card, cell, lane, storiesBoard } from '../../../features/board/ct/fixtures';

// Boards for the phone detail shots and checks in boardDetails.phone.spec.tsx (SHIP-1828): the inline child list, the
// split-column captions, lanes by Epic, the drop-target outline and the card header with points hidden.

/** storiesBoard() with two cards in the default lane's New cell (the first page), so one can be held over the other. */
export function boardWithTwoNew(): BoardViewDto {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: [
              cell('col-new', [
                card({ key: 'SHIP-163', title: 'Saved cards on the payment step', points: 5 }),
                card({ key: 'SHIP-164', title: 'Receipt email shows the wrong currency', points: 3 }),
              ]),
              ...candidate.cells.slice(1),
            ],
          }),
    ),
  };
}

/** storiesBoard() with an Expedite card in Ready's Doing half, so the split column shows two lanes. */
export function boardWithExpediteInReady(): BoardViewDto {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-expedite'
        ? candidate
        : lane({
            ...candidate,
            cardCount: 2,
            cells: candidate.cells.map((c, i) =>
              i === 1
                ? cell('col-ready', [
                    card({ key: 'SHIP-151', title: 'Fix payout rounding on refunds', points: 3 }),
                  ])
                : c,
            ),
          }),
    ),
  };
}

/** storiesBoard() with a board style that hides points on every card type it sets. */
export function boardWithPointsHidden(): BoardViewDto {
  const base = storiesBoard();
  return { ...base, cardFields: base.cardFields.map((f) => ({ ...f, showPoints: false })) };
}
