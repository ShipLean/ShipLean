import { useMemo, useState, type ComponentType } from 'react';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { highlightJson } from './highlightJson';
import { toMcpJson } from './toMcpJson';

const OVERLINE =
  'mb-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase';

/** JSON for agents: what inspection_metrics returns for the same range and split, highlighted, with a Copy button. */
export const JsonView: ComponentType<InspectionViewProps> = ({ project, view, onQueryChange }) => {
  const text = useMemo(() => JSON.stringify(toMcpJson(view), null, 2), [view]);
  const html = useMemo(() => highlightJson(text), [text]);
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(text).then(
      () => setCopied(true),
      () => setCopied(false),
    );
  };
  const call = `inspection_metrics(project: "${project.key}", range: "${view.range}", cut: "${view.cut}")`;
  return (
    <section data-testid={ids.json} className="grid gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          className="btn-ghost"
          onClick={() => onQueryChange({ view: 'overview' })}
        >
          ← Overview
        </button>
        <h2 className="text-[length:var(--text-lg)] font-semibold">JSON for agents</h2>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          what the MCP tool returns for the same view
        </span>
      </div>
      <div className="grid grid-cols-[minmax(0,1fr)_260px] items-start gap-3">
        <div className="panel grid gap-2 p-3">
          <div className="flex items-center gap-3">
            <code className="font-mono text-[length:var(--text-xs)] text-ink-muted">{call}</code>
            <span className="flex-1" />
            <button
              data-testid={ids.jsonCopy}
              type="button"
              onClick={copy}
              className="rounded-md border border-edge bg-panel px-2.5 py-1 text-[length:var(--text-sm)] text-ink"
            >
              {copied ? 'Copied' : 'Copy'}
            </button>
          </div>
          <pre
            data-testid={ids.jsonBody}
            aria-label={`inspection_metrics response for ${project.key}`}
            className="m-0 max-h-[calc(100vh-260px)] overflow-auto rounded-md border border-edge bg-sunken p-3 text-[length:var(--text-xs)] [&_.json-b]:text-signal [&_.json-k]:text-ink [&_.json-n]:text-signal [&_.json-s]:text-ink-muted"
            dangerouslySetInnerHTML={{ __html: html }}
          />
        </div>
        <aside className="panel grid gap-3 p-3 text-[length:var(--text-sm)]">
          <section>
            <div className={OVERLINE}>Who reads it</div>
            <p className="m-0">
              Retro and planning agents: they can quote the numbers, find the slowest tasks and open
              each one's timeline with <code>feature_timeline</code>.
            </p>
          </section>
          <section>
            <div className={OVERLINE}>Same model as the page</div>
            <p className="m-0">
              The page and the tool share one view model, so a person and an agent never see
              different numbers. <code>aggregate</code> says the headline is a mean.
            </p>
          </section>
        </aside>
      </div>
    </section>
  );
};
