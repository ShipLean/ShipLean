import { describe, expect, it } from 'vitest';
import type { BoardMoves } from '../../moves/BoardMovesContext';
import { CardActionsContext, useCardActions } from './CardActionsContext';

describe('cardActionsSeams', () => {
  it('cardActionsSeams_ShouldExportTheWave2Names', () => {
    expect(typeof useCardActions).toBe('function');
    expect(CardActionsContext.Provider).toBeDefined();
    // moveTo is on BoardMoves: a type-level check, a wrong shape fails typecheck.
    const moveTo: BoardMoves['moveTo'] = (itemId, to) => void [itemId, to.columnId];
    expect(typeof moveTo).toBe('function');
  });
});
