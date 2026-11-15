import { Markdown } from '../../components/Markdown';
import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

/** What the agent said, by chapter: the server's sanitised HTML, or a line saying there is none. */
export function DemoScript({ demo }: DemoProps) {
  const { scriptHtml } = demo;
  return (
    <section aria-label="Script" className="min-w-0 rounded-panel border border-edge bg-surface">
      <header className="flex min-h-9 items-center gap-2 border-b border-edge px-3">
        <h2 className="text-[length:var(--text-base)] font-semibold">Script</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          What the agent said, by chapter
        </span>
      </header>
      <div data-testid={ids.demoScript} className="max-w-205 px-4 py-3 touch:px-3">
        {scriptHtml === null || scriptHtml === '' ? (
          <p className="text-[length:var(--text-sm)] text-ink-muted">This demo has no script.</p>
        ) : (
          <Markdown html={scriptHtml} />
        )}
      </div>
    </section>
  );
}
