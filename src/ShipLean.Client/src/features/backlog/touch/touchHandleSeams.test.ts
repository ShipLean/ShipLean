import { describe, expect, it } from 'vitest';
import { TouchHandle } from './TouchHandle';
import { useTouchReorder } from './useTouchReorder';

// Wave 2 (List, Planning, Tree) imports these by name: a rename fails here first.
describe('touch handle seams', () => {
  it('touchHandleSeams_ShouldExportTheWave2Names', () => {
    for (const seam of [TouchHandle, useTouchReorder]) {
      expect(typeof seam).toBe('function');
    }
  });
});
