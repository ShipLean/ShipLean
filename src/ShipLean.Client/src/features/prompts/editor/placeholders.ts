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
  // Lines keep their own ending: a trailing "\r" is carried by the line, and compared without it.
  const lines = text.split('\n');
  const bare = lines.map(stripCr);
  const builtIn = builtInText.replace(/\r\n/g, '\n').split('\n');
  const at = builtIn.findIndex((line) => line.includes(placeholder));
  const insertion = at < 0 ? placeholder : builtIn[at]!;
  const position = anchor(bare, builtIn, at);
  const trailing = lines.length > 1 && lines[lines.length - 1] === '';
  const index = position >= 0 ? position : trailing ? lines.length - 1 : lines.length;
  if (index === lines.length) {
    // Appended after a last line with no terminator: that line gets the text's ending, the new one stays bare.
    if (text.includes('\r\n')) lines[index - 1] = `${lines[index - 1]}\r`;
    lines.push(insertion);
  } else {
    // Inserted mid-text: the new line ends like the line before it.
    const crlf = lines[index - 1]?.endsWith('\r') ?? text.includes('\r\n');
    lines.splice(index, 0, crlf ? `${insertion}\r` : insertion);
  }
  return lines.join('\n');
}

function stripCr(line: string): string {
  return line.endsWith('\r') ? line.slice(0, -1) : line;
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
