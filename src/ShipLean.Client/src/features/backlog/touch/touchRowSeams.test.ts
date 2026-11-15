import { describe, expect, it } from 'vitest';
import { TouchSelectModeProvider, useTouchSelectMode } from './selectMode';
import { TouchRow } from './TouchRow';
import { TouchRowList } from './TouchRowList';

// Wave 2 (Planning, Tree, row ⋯, Select mode, the List handle drag) imports these by name: a rename fails here first.
describe('touch row seams', () => {
  it('touchRowSeams_ShouldExportTheWave2Names', () => {
    for (const seam of [TouchRow, TouchRowList, TouchSelectModeProvider, useTouchSelectMode]) {
      expect(typeof seam).toBe('function');
    }
  });
});
