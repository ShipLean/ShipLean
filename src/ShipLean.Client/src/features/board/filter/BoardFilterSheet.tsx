import { useRef, useState } from 'react';
import type { Level } from '../../../routes';
import { Sheet } from '../../../touch/Sheet';
import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { BoardFilterFields } from './BoardFilterFields';
import { filterCount } from './filterQuery';

// The filter at 900px and below: a bottom sheet with the keyword, people (or owner), and Sprint or Epic as a native select.
// A change applies as it is made; Show cards (and Close) put the sheet away, and apply a keyword typed in the last moment.
// The keyword box is not focused until it is tapped (C4).
export function BoardFilterSheet({
  open,
  projectId,
  level,
  filter,
  onChange,
  onClose,
}: {
  open: boolean;
  projectId: string;
  level: Level;
  filter: BoardFilterQuery;
  onChange: (filter: BoardFilterQuery) => void;
  onClose: () => void;
}) {
  const [clears, setClears] = useState(0);
  const draft = useRef<string | null>(null);

  function close() {
    const text = draft.current?.trim();
    draft.current = null;
    if (text !== undefined && text !== (filter.q ?? '')) {
      onChange({ ...filter, q: text === '' ? undefined : text });
    }
    onClose();
  }

  return (
    <Sheet
      open={open}
      title="Filter"
      onClose={close}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.filterClear}
            className="btn-ghost touch:min-h-[var(--touch-target)]"
            disabled={filterCount(filter) === 0}
            onClick={() => {
              draft.current = null;
              setClears((count) => count + 1);
              onChange({});
            }}
          >
            Clear
          </button>
          <button
            type="button"
            data-testid={ids.filterShow}
            className="btn-signal flex-1 touch:min-h-[var(--touch-target)]"
            onClick={close}
          >
            Show cards
          </button>
        </>
      }
    >
      <div data-testid={ids.filterSheet} className="grid gap-3 pb-1">
        <BoardFilterFields
          projectId={projectId}
          level={level}
          filter={filter}
          onChange={onChange}
          keywordKey={clears}
          labelled
          keywordClass="field w-full"
          onKeywordDraft={(text) => {
            draft.current = text;
          }}
        />
      </div>
    </Sheet>
  );
}
