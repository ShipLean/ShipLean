export type SpanKind = 'plain' | 'heading' | 'placeholder' | 'agentTag';

export interface SourceSpan {
  kind: SpanKind;
  text: string;
}

export interface SourceLine {
  number: number;
  spans: SourceSpan[];
}

const headingLine = /^\s{0,3}#{1,6}(\s|$)/;
const agentTagLine = /^\s*<\/?(claude-code|codex-cli|cursor)>\s*$/;
const placeholder = /\{\{[^{}\n]+\}\}/g;

function splitPlaceholders(text: string): SourceSpan[] {
  const spans: SourceSpan[] = [];
  let from = 0;
  for (const match of text.matchAll(placeholder)) {
    if (match.index > from) spans.push({ kind: 'plain', text: text.slice(from, match.index) });
    spans.push({ kind: 'placeholder', text: match[0] });
    from = match.index + match[0].length;
  }
  if (from < text.length) spans.push({ kind: 'plain', text: text.slice(from) });
  return spans;
}

/** One line as spans: a heading line and an agent tag on its own line are one span; `{{placeholders}}` are found elsewhere. */
export function highlightLine(line: string): SourceSpan[] {
  if (headingLine.test(line)) return [{ kind: 'heading', text: line }];
  if (agentTagLine.test(line)) return [{ kind: 'agentTag', text: line }];
  return splitPlaceholders(line);
}

/** The whole text as numbered lines of spans (a trailing newline does not add an empty line). */
export function highlightSource(text: string): SourceLine[] {
  const lines = text.split('\n');
  if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
  return lines.map((line, i) => ({ number: i + 1, spans: highlightLine(line) }));
}

/** True when a line of `text` is an agent tag on its own line. */
export const hasAgentTagLines = (text: string) =>
  text.split('\n').some((line) => agentTagLine.test(line));
