import { expect, test } from 'vitest';
import { columnsOf } from './levelColumns';

test.each(['features', 'epics'] as const)(
  'columnsOf_WhenLevelIs%s_ShouldLabelProgressWithTheTypesItCounts',
  (level) => {
    const progress = columnsOf(level).find((column) => column.id === 'progress');
    expect(progress?.label).toBe('Progress (stories, bugs and Action items)');
  },
);
