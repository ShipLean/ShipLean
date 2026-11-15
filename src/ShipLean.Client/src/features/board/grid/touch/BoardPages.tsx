import { useMemo, useState } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { TouchPager } from '../../../../touch/TouchPager';
import { boardTestIds as ids } from '../../testIds';
import type { BoardContext } from '../boardContext';
import { BoardPagerContext, type BoardPager } from './BoardPagerContext';
import { ColumnPage } from './ColumnPage';
import { pagesOf } from './pagesOf';

// The Stories, Features and Epics boards at 900px and below (SHIP-719 D1, D7-D9): one column a page in the touch pager,
// column chips above. The chosen column and the collapsed lanes are kept here, so a refetch or closing the item modal
// leaves the page on the column you were reading and ▼ folds a lane in every column.
export function BoardPages({
  view,
  context,
  headerFlashes,
}: {
  view: BoardViewDto;
  context: BoardContext;
  headerFlashes: number;
}) {
  const [chosen, setChosen] = useState<string | undefined>(undefined);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const pages = useMemo(() => pagesOf(view, collapsed), [view, collapsed]);
  const columns = view.columns;
  const current = columns.some((c) => c.id === chosen) ? chosen! : (columns[0]?.id ?? '');
  const index = columns.findIndex((c) => c.id === current);
  const pager: BoardPager = {
    columnId: current,
    turn: (dir) =>
      setChosen(
        columns[Math.min(Math.max(index + (dir === 'left' ? -1 : 1), 0), columns.length - 1)]?.id,
      ),
    goTo: setChosen,
  };
  const toggle = (laneId: string) =>
    setCollapsed((now) => {
      const next = new Set(now);
      if (!next.delete(laneId)) next.add(laneId);
      return next;
    });

  return (
    <BoardPagerContext.Provider value={pager}>
      <div data-testid={ids.pagerGrid} className="min-w-0 pb-4">
        <TouchPager
          pages={columns.map((column) => ({
            id: column.id,
            label: column.name,
            count: column.count,
            limit: column.wipLimit ?? undefined,
          }))}
          current={current}
          onPageChange={setChosen}
          renderPage={(id) => {
            const data = pages.find((page) => page.column.id === id);
            return data ? (
              <ColumnPage
                data={data}
                context={context}
                headerFlashes={headerFlashes}
                onToggle={toggle}
              />
            ) : null;
          }}
        />
      </div>
    </BoardPagerContext.Provider>
  );
}
