import { Markdown } from '../../../../components/Markdown';
import { useRenderedMarkdown } from '../../../workItem/workItemApi';
import { boardSettingsTestIds as ids } from '../../testIds';

/** The server's render of a column's definition of done, as the board shows it. */
export function DefinitionOfDonePreview({ name, markdown }: { name: string; markdown: string }) {
  const blank = markdown.trim() === '';
  const rendered = useRenderedMarkdown(markdown, !blank);
  return (
    <div
      data-testid={ids.columnDodPreview}
      className="rounded-control border border-edge bg-panel px-3 py-2 text-[length:var(--text-sm)]"
    >
      <div className="mb-1.5 border-b border-edge pb-1.5 font-semibold">{name}</div>
      {blank ? (
        <span className="text-ink-muted">Nothing yet</span>
      ) : (
        <Markdown html={rendered.data?.html ?? ''} />
      )}
    </div>
  );
}
