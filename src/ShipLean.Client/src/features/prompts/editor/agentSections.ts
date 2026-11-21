/** The agent ids a section tag can name (C5), in the order ties resolve. */
export const AGENT_IDS = ['claude-code', 'codex-cli', 'cursor'] as const;
export type AgentId = (typeof AGENT_IDS)[number];

export type AgentSectionProblemKind = 'unclosed' | 'nested' | 'strayClose' | 'unknownTag';

export interface AgentSectionProblem {
  line: number;
  kind: AgentSectionProblemKind;
  tag: string;
  suggestion: string | null;
}

interface Line {
  text: string;
  isTag: boolean;
  isClose: boolean;
  name: string;
  isKnown: boolean;
}

const MAX_SUGGESTION_DISTANCE = 3;
const NOT_A_TAG = (text: string): Line => ({
  text,
  isTag: false,
  isClose: false,
  name: '',
  isKnown: false,
});
// .NET's string.Trim() set (char.IsWhiteSpace): not U+FEFF, but U+0085.
const DOTNET_WHITESPACE =
  '\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000';
const DOTNET_TRIM = new RegExp(`^[${DOTNET_WHITESPACE}]+|[${DOTNET_WHITESPACE}]+$`, 'g');

/** Trims exactly as the server's string.Trim() does, so both sides see the same tag lines. */
export function dotnetTrim(text: string): string {
  return text.replace(DOTNET_TRIM, '');
}

const TAG_NAME = /^[a-z][a-z0-9-]*$/;

function fenceMarker(trimmed: string): string {
  if (trimmed.startsWith('```')) return '`';
  return trimmed.startsWith('~~~') ? '~' : '';
}

function fenceClose(raws: string[], from: number, marker: string): number {
  for (let index = from; index < raws.length; index++) {
    if (fenceMarker(dotnetTrim(raws[index]!)) === marker) return index;
  }
  return -1;
}

function parseTag(raw: string): Line {
  const trimmed = dotnetTrim(raw);
  if (trimmed.length < 3 || !trimmed.startsWith('<') || !trimmed.endsWith('>')) {
    return NOT_A_TAG(raw);
  }
  const isClose = trimmed[1] === '/';
  const name = trimmed.slice(isClose ? 2 : 1, -1);
  if (!TAG_NAME.test(name)) return NOT_A_TAG(raw);
  return {
    text: raw,
    isTag: true,
    isClose,
    name,
    isKnown: (AGENT_IDS as readonly string[]).includes(name),
  };
}

// A fence that is never closed is not a fence: only a closed one makes its lines text.
function scan(text: string): Line[] {
  const raws = text.replace(/\r\n/g, '\n').split('\n');
  const result: Line[] = [];
  let index = 0;
  while (index < raws.length) {
    const marker = fenceMarker(dotnetTrim(raws[index]!));
    const close = marker === '' ? -1 : fenceClose(raws, index + 1, marker);
    if (close < 0) {
      result.push(parseTag(raws[index]!));
      index++;
    } else {
      for (; index <= close; index++) result.push(NOT_A_TAG(raws[index]!));
    }
  }
  return result;
}

function distance(a: string, b: string): number {
  let previous = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    const current = [i];
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      current[j] = Math.min(current[j - 1]! + 1, previous[j]! + 1, previous[j - 1]! + cost);
    }
    previous = current;
  }
  return previous[b.length]!;
}

function suggest(name: string): string | null {
  let best: string | null = null;
  let bestDistance = MAX_SUGGESTION_DISTANCE + 1;
  for (const id of AGENT_IDS) {
    const d = distance(name, id);
    if (d < bestDistance) {
      best = id;
      bestDistance = d;
    }
  }
  return best;
}

function problemsOf(lines: Line[]): AgentSectionProblem[] {
  const problems: AgentSectionProblem[] = [];
  let open: string | null = null;
  let openLine = 0;
  lines.forEach((line, index) => {
    const number = index + 1;
    if (!line.isTag) return;
    if (!line.isKnown) {
      const suggestion = suggest(line.name);
      if (suggestion !== null) {
        problems.push({ line: number, kind: 'unknownTag', tag: line.name, suggestion });
      }
    } else if (!line.isClose && open !== null) {
      problems.push({ line: number, kind: 'nested', tag: line.name, suggestion: null });
    } else if (!line.isClose) {
      open = line.name;
      openLine = number;
    } else if (open === null || open !== line.name) {
      problems.push({ line: number, kind: 'strayClose', tag: line.name, suggestion: null });
    } else {
      open = null;
    }
  });
  if (open !== null) {
    problems.push({ line: openLine, kind: 'unclosed', tag: open, suggestion: null });
  }
  return problems.sort((a, b) => a.line - b.line);
}

/** Everything the agent gets: untagged text plus its own sections; an error drops every region (D20). */
export function filterSections(text: string, agent: string): string {
  const lines = scan(text);
  const hasError = problemsOf(lines).some((problem) => problem.kind !== 'unknownTag');
  const kept: string[] = [];
  let section: string | null = null;
  for (const line of lines) {
    if (section === null && line.isTag && line.isKnown) {
      section = line.isClose ? null : line.name;
    } else if (section !== null) {
      // A region ends only at the close tag of the open tag that started it.
      if (line.isTag && line.isKnown && line.isClose && line.name === section) section = null;
      if (!hasError && section === agent && !(line.isTag && line.isKnown)) kept.push(line.text);
    } else {
      kept.push(line.text);
    }
  }
  return kept.join('\n');
}

export function checkSections(text: string): AgentSectionProblem[] {
  return problemsOf(scan(text));
}

/** Opening `<id>` tags per agent id, for the side panel's counts. */
export function countSections(text: string): Record<AgentId, number> {
  const counts: Record<AgentId, number> = { 'claude-code': 0, 'codex-cli': 0, cursor: 0 };
  for (const line of scan(text)) {
    if (line.isTag && line.isKnown && !line.isClose) counts[line.name as AgentId]++;
  }
  return counts;
}

/** Replaces the tag on a 1-based line (open or close form) with the suggested id, keeping its indentation and line ending. */
export function fixTagLine(text: string, line: number, suggestion: string): string {
  const raws = text.split('\n');
  const raw = raws[line - 1];
  if (raw === undefined) return text;
  const ending = raw.endsWith('\r') ? '\r' : '';
  const body = ending === '' ? raw : raw.slice(0, -1);
  const close = dotnetTrim(body).startsWith('</');
  const indent = body.slice(0, body.indexOf('<'));
  raws[line - 1] = `${indent}<${close ? '/' : ''}${suggestion}>${ending}`;
  return raws.join('\n');
}
