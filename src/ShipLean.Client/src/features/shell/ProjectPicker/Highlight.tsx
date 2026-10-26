// The text with the first occurrence of the search marked.
import { shellTestIds as ids } from '../testIds';

export function Highlight({ text, search }: { text: string; search: string }) {
  const query = search.trim().toLowerCase();
  const at = query === '' ? -1 : text.toLowerCase().indexOf(query);
  if (at < 0) return text;
  return (
    <>
      {text.slice(0, at)}
      <mark data-testid={ids.pickerMatch} className="rounded-[2px] bg-caution/30 text-inherit">
        {text.slice(at, at + query.length)}
      </mark>
      {text.slice(at + query.length)}
    </>
  );
}
