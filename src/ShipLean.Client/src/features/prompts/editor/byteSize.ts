/** The most a prompt's text may be: 64 KB of UTF-8 (D10, AC5). */
export const MAX_PROMPT_BYTES = 65536;

const encoder = new TextEncoder();

/** The size the server measures (P11): UTF-8 bytes after `\r\n` becomes `\n`. */
export function promptByteSize(text: string): number {
  return encoder.encode(text.replaceAll('\r\n', '\n')).length;
}
