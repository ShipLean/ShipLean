const TOKEN = /\{\{[A-Za-z0-9_]+\}\}/g;

function tokens(text: string): Set<string> {
  return new Set(text.match(TOKEN) ?? []);
}

/** Placeholders the prompt expects that the text no longer contains. */
export function missingPlaceholders(text: string, placeholders: readonly string[]): string[] {
  const used = tokens(text);
  return placeholders.filter((placeholder) => !used.has(placeholder));
}

/** `{{name}}` tokens in the text that ShipLean does not fill in for this prompt. */
export function unknownPlaceholders(text: string, placeholders: readonly string[]): string[] {
  return [...tokens(text)].filter((token) => !placeholders.includes(token));
}

/**
 * Re-inserts a placeholder where the built-in has it: the built-in line that holds it is placed after the nearest
 * unchanged line before it (else before the nearest unchanged line after it), else the line is appended at the end.
 */
export function putBack(text: string, placeholder: string, builtInText: string): string {
  const eol = text.includes('\r\n') ? '\r\n' : '\n';
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  const builtIn = builtInText.replace(/\r\n/g, '\n').split('\n');
  const at = builtIn.findIndex((line) => line.includes(placeholder));
  const insertion = at < 0 ? placeholder : builtIn[at]!;
  const position = anchor(lines, builtIn, at);
  if (position < 0) {
    const trailing = lines.length > 0 && lines[lines.length - 1] === '';
    lines.splice(trailing ? lines.length - 1 : lines.length, 0, insertion);
  } else {
    lines.splice(position, 0, insertion);
  }
  return lines.join(eol);
}

// The index in `lines` to insert at, or -1 when no surrounding built-in line survives unchanged.
function anchor(lines: string[], builtIn: string[], at: number): number {
  if (at < 0) return -1;
  for (let i = at - 1; i >= 0; i--) {
    const found = surviving(lines, builtIn[i]!);
    if (found >= 0) return found + 1;
  }
  for (let i = at + 1; i < builtIn.length; i++) {
    const found = surviving(lines, builtIn[i]!);
    if (found >= 0) return found;
  }
  return -1;
}

function surviving(lines: string[], line: string): number {
  return line.trim() === '' ? -1 : lines.indexOf(line);
}
