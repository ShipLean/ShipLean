import { describe, expect, it } from 'vitest';
import { BoardPagerContext, useBoardPager } from './BoardPagerContext';

describe('boardPagerSeams', () => {
  it('boardPagerSeams_ShouldExportUseBoardPager', () => {
    expect(typeof useBoardPager).toBe('function');
    expect(BoardPagerContext.Provider).toBeDefined();
  });
});
