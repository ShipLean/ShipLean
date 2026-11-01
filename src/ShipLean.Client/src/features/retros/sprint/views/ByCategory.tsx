import { useState } from 'react';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { useRetroThemes } from '../../useRetroQueries';
import { CategoryColumn } from './byCategory/CategoryColumn';
import { useRetroPointWrites } from './byCategory/useRetroPointWrites';
import { filterColumns, type WrittenBy } from './byCategory/writtenBy';
import { WrittenByFilter } from './byCategory/WrittenByFilter';

/** The By category view: the Written by filter over four columns (Went badly, Can be improved, Not in the plan, Went well), each grouped by theme. */
export function ByCategory({ project, retro }: SprintRetroSlotProps) {
  const [writtenBy, setWrittenBy] = useState<WrittenBy>('all');
  const themes = useRetroThemes(project.id);
  const writes = useRetroPointWrites(project.id);
  const columns = filterColumns(retro.categories, writtenBy);
  return (
    <div className="grid gap-3">
      <WrittenByFilter value={writtenBy} onChange={setWrittenBy} />
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        {columns.map((column) => (
          <CategoryColumn
            key={column.category}
            column={column}
            themes={themes.data}
            writes={writes}
          />
        ))}
      </div>
    </div>
  );
}
