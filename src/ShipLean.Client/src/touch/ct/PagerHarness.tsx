import { useState } from 'react';
import { TouchPager, type TouchPagerPage } from '../TouchPager';
import { pagerHarnessIds as ids } from './pagerHarnessIds';

const initial: TouchPagerPage[] = [
  { id: 'todo', label: 'To do', count: 3, limit: 5 },
  { id: 'doing', label: 'Doing', count: 4, limit: 3 },
  { id: 'review', label: 'Review', count: 2 },
  { id: 'done', label: 'Done', count: 12, pulse: 5 },
];

// Tall and short pages, so the track's height follows the tallest and the document scrolls vertically.
const rows: Record<string, number> = { todo: 3, doing: 14, review: 2, done: 8 };

export function PagerHarness() {
  const [pages, setPages] = useState(initial);
  const [current, setCurrent] = useState('todo');
  const [reports, setReports] = useState<string[]>([]);
  const [inserted, setInserted] = useState(0);
  const [, setTick] = useState(0);

  const insertBefore = () => {
    const at = pages.findIndex((page) => page.id === current);
    const id = `new-${inserted}`;
    setInserted(inserted + 1);
    const next = [...pages];
    next.splice(at, 0, { id, label: `New ${inserted}`, count: 0 });
    setPages(next);
  };

  return (
    <div>
      <div className="flex gap-2 p-2">
        <button data-testid={ids.ctPagerInsert} onClick={insertBefore}>
          Insert before
        </button>
        <button data-testid={ids.ctPagerGoToLast} onClick={() => setCurrent('done')}>
          Go to Done
        </button>
        <button data-testid={ids.ctPagerRerender} onClick={() => setTick((n) => n + 1)}>
          Re-render
        </button>
        <button
          data-testid={ids.ctPagerPulse}
          onClick={() =>
            setPages((now) =>
              now.map((page) =>
                page.id === 'review' ? { ...page, pulse: (page.pulse ?? 0) + 1 } : page,
              ),
            )
          }
        >
          Pulse Review
        </button>
      </div>
      <p data-testid={ids.ctPagerCurrent}>{current}</p>
      <p data-testid={ids.ctPagerReports}>{reports.join(',')}</p>
      <TouchPager
        pages={pages.map((page) => ({ ...page }))}
        current={current}
        onPageChange={(id) => {
          setCurrent(id);
          setReports((now) => [...now, id]);
        }}
        renderPage={(id) => (
          <div
            data-testid={ids.ctPagerPageBody}
            className="grid gap-2 rounded border border-border bg-surface p-2"
          >
            {Array.from({ length: rows[id] ?? 1 }, (_, i) => (
              <div key={i} className="h-10 rounded bg-accent-soft p-2 text-sm">
                {id} card {i + 1}
              </div>
            ))}
          </div>
        )}
      />
    </div>
  );
}
