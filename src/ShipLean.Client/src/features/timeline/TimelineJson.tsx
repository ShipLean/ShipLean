import { useMemo, useState } from 'react';
import { highlightJson } from './highlightJson';
import { timelineTestIds as ids } from './testIds';
import type { TimelineJsonProps } from './TimelineJsonProps';
import { toMcpJson } from './toMcpJson';

/** The view the way the feature_timeline tool returns it, highlighted, with a button that copies the text. */
export function TimelineJson({ view }: TimelineJsonProps) {
  const text = useMemo(() => JSON.stringify(toMcpJson(view), null, 2), [view]);
  const html = useMemo(() => highlightJson(text), [text]);
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(text).then(
      () => setCopied(true),
      () => setCopied(false),
    );
  };
  return (
    <section data-testid={ids.json} className="relative flex min-h-full flex-col p-4">
      <div className="mb-2 flex items-center gap-3 text-[length:var(--text-sm)] text-ink-muted">
        <span data-testid={ids.jsonFieldGuide}>
          This is what <code>feature_timeline</code> returns for {view.feature.key}.
        </span>
        <span className="flex-1" />
        <button
          data-testid={ids.jsonCopy}
          type="button"
          onClick={copy}
          className="rounded-md border border-edge bg-panel px-2.5 py-1 text-ink"
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre
        aria-label={`feature_timeline response for ${view.feature.key}`}
        className="m-0 flex-1 overflow-auto whitespace-pre rounded-md border border-edge bg-panel px-4 py-3 font-mono text-[12px] leading-[1.55] text-ink [tab-size:2] [&_.json-b]:text-[color:var(--color-purple)] [&_.json-k]:text-[color:var(--color-accent)] [&_.json-n]:text-[color:color-mix(in_srgb,var(--color-warn)_70%,var(--color-text))] [&_.json-s]:text-[color:color-mix(in_srgb,var(--color-ok)_70%,var(--color-text))]"
        dangerouslySetInnerHTML={{ __html: html }}
      />
    </section>
  );
}
