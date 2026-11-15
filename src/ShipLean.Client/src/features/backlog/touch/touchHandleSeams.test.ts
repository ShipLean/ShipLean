import type { ComponentProps } from 'react';
import { describe, expect, it } from 'vitest';
import { TouchHandle } from './TouchHandle';
import { TouchRowList } from './TouchRowList';
import { useTouchReorder } from './useTouchReorder';

type HandleDrop = NonNullable<ComponentProps<typeof TouchRowList>['onHandleDrop']>;
type ReorderDrop = ReturnType<typeof useTouchReorder>['drop'];

// Wave 2 (List, Planning, Tree) imports these by name: a rename fails here first.
describe('touch handle seams', () => {
  it('touchHandleSeams_ShouldExportTheWave2Names', () => {
    for (const seam of [TouchHandle, useTouchReorder, TouchRowList]) {
      expect(typeof seam).toBe('function');
    }
  });

  it('touchHandleSeams_WhenReorderDropIsPassed_ShouldFitTouchRowListOnHandleDrop', () => {
    // What ListScreen does: onHandleDrop={touchReorder.drop}. A signature drift fails typecheck here.
    const fits = (drop: ReorderDrop): HandleDrop => drop;
    expect(typeof fits).toBe('function');
  });
});
