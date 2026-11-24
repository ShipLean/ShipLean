import { promptsTestIds as ids } from '../../testIds';
import { highlightSource, type SpanKind } from './highlight';

const spanClass: Record<SpanKind, string> = {
  plain: '',
  heading: 'font-semibold text-signal',
  placeholder: 'rounded-sm bg-label-purple/12 text-label-purple',
  agentTag: 'rounded-sm bg-notice/13 font-semibold text-notice',
};

const spanTestId: Record<SpanKind, string | undefined> = {
  plain: undefined,
  heading: ids.sourceHeading,
  placeholder: ids.sourcePlaceholder,
  agentTag: ids.sourceAgentTag,
};

function Spans({ line }: { line: ReturnType<typeof highlightSource>[number] }) {
  return (
    <>
      {line.spans.map((span, i) => (
        <span key={i} data-testid={spanTestId[span.kind]} className={spanClass[span.kind]}>
          {span.text}
        </span>
      ))}
      {line.spans.length === 0 ? ' ' : null}
    </>
  );
}

/** Markdown source in monospace. Boxed (the default): a scrolling sunken block with line numbers. Bleed: straight in
 * the panel body, no box and no line numbers, in larger text (the source view, D13). Spans are React text nodes, never
 * HTML. */
export function SourceBlock(props: {
  text: string;
  testId?: string;
  label: string;
  bleed?: boolean;
}) {
  const lines = highlightSource(props.text);
  if (props.bleed === true)
    return (
      <pre
        data-testid={props.testId ?? ids.sourceText}
        aria-label={props.label}
        tabIndex={0}
        className="m-0 font-[family-name:var(--font-mono)] text-[length:var(--text-sm)] leading-[1.55] whitespace-pre-wrap text-ink [overflow-wrap:anywhere]"
      >
        {lines.map((line) => (
          <div key={line.number}>
            <Spans line={line} />
          </div>
        ))}
      </pre>
    );
  return (
    <pre
      data-testid={props.testId ?? ids.sourceText}
      aria-label={props.label}
      tabIndex={0}
      className="m-0 max-h-[32rem] overflow-auto rounded-control border border-edge bg-sunken p-3 font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] leading-relaxed"
    >
      {lines.map((line) => (
        <div key={line.number} className="grid grid-cols-[3rem_minmax(0,1fr)] gap-2">
          <span
            aria-hidden="true"
            data-testid={ids.sourceLineNumber}
            className="select-none text-right text-ink-muted"
          >
            {line.number}
          </span>
          <span className="whitespace-pre-wrap [overflow-wrap:anywhere]">
            <Spans line={line} />
          </span>
        </div>
      ))}
    </pre>
  );
}
