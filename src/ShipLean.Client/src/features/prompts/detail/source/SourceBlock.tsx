import { promptsTestIds as ids } from '../../testIds';
import { highlightSource, type SpanKind } from './highlight';

const spanClass: Record<SpanKind, string> = {
  plain: '',
  heading: 'font-semibold text-signal',
  placeholder: 'rounded-sm bg-purple/10 text-purple',
  agentTag: 'rounded-sm bg-info/10 font-semibold text-info',
};

const spanTestId: Record<SpanKind, string | undefined> = {
  plain: undefined,
  heading: ids.sourceHeading,
  placeholder: ids.sourcePlaceholder,
  agentTag: ids.sourceAgentTag,
};

/** Markdown source in a scrolling monospace block with line numbers. Spans are React text nodes, never HTML. */
export function SourceBlock(props: { text: string; testId?: string; label: string }) {
  const lines = highlightSource(props.text);
  return (
    <pre
      data-testid={props.testId ?? ids.sourceText}
      aria-label={props.label}
      tabIndex={0}
      className="m-0 max-h-[32rem] overflow-auto rounded-control border border-edge bg-sunken p-3 font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] leading-relaxed"
    >
      {lines.map((line) => (
        <div key={line.number} className="grid grid-cols-[3rem_minmax(0,1fr)] gap-2">
          <span aria-hidden="true" className="select-none text-right text-ink-muted">
            {line.number}
          </span>
          <span className="whitespace-pre-wrap [overflow-wrap:anywhere]">
            {line.spans.map((span, i) => (
              <span key={i} data-testid={spanTestId[span.kind]} className={spanClass[span.kind]}>
                {span.text}
              </span>
            ))}
            {line.spans.length === 0 ? ' ' : null}
          </span>
        </div>
      ))}
    </pre>
  );
}
