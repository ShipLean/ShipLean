const extensions: Record<string, string> = {
  'image/png': 'png',
  'image/jpeg': 'jpg',
  'image/gif': 'gif',
  'image/webp': 'webp',
  'image/svg+xml': 'svg',
};

/** The image files on a clipboard, in order. */
export const imageFiles = (files: Iterable<File>): File[] =>
  [...files].filter((file) => file.type.startsWith('image/'));

/**
 * The name each pasted file attaches under: its own when it has one the item doesn't already hold, otherwise the next
 * free `pasted-<n>.<ext>`. Uploading over a name replaces that file, so a name never repeats within the item.
 */
export function pastedNames(files: File[], taken: string[]): string[] {
  const used = new Set(taken.map((name) => name.toLowerCase()));
  let n = 0;
  return files.map((file) => {
    let name = file.name;
    if (name === '' || used.has(name.toLowerCase())) {
      const extension = extensions[file.type] ?? 'png';
      do {
        n += 1;
        name = `pasted-${n}.${extension}`;
      } while (used.has(name));
    }
    used.add(name.toLowerCase());
    return name;
  });
}

/** `text` with `![](name)` for each name in place of the selection (clamped to the text), and the cursor after them. */
export function insertReferences(text: string, start: number, end: number, names: string[]) {
  const from = Math.min(start, text.length);
  const to = Math.min(Math.max(end, from), text.length);
  const inserted = names.map((name) => `![](${name})`).join('\n');
  return { text: text.slice(0, from) + inserted + text.slice(to), cursor: from + inserted.length };
}
