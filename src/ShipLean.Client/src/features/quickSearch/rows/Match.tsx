// The text with the first occurrence of the query marked.
import { quickSearchTestIds as ids } from '../testIds';

export function Match({ text, query }: { text: string; query: string }) {
  const needle = query.trim().toLowerCase();
  const at = needle === '' ? -1 : text.toLowerCase().indexOf(needle);
  if (at < 0) return text;
  return (
    <>
      {text.slice(0, at)}
      <mark data-testid={ids.match} className="rounded-[2px] bg-caution/30 text-inherit">
        {text.slice(at, at + needle.length)}
      </mark>
      {text.slice(at + needle.length)}
    </>
  );
}
