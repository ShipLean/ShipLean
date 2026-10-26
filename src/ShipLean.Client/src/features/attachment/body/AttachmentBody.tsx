import type { AttachmentMarkdownDto } from '../../../api/attachments';
import { Markdown } from '../../../components/Markdown';
import type { AttachmentView } from '../header/ViewToggle';
import { attachmentTestIds as ids } from '../testIds';

// The file, rendered (the server's sanitised HTML, image placeholders styled by Markdown) or as its source text.
export function AttachmentBody({
  data,
  view,
}: {
  data: AttachmentMarkdownDto;
  view: AttachmentView;
}) {
  return view === 'rendered' ? (
    <article data-testid={ids.body} className="max-w-190 px-5 py-4 leading-relaxed">
      <Markdown html={data.html} />
    </article>
  ) : (
    <pre
      data-testid={ids.sourceText}
      className="m-4 max-w-215 rounded-control border border-edge bg-sunken p-3 font-[family-name:var(--font-mono)] text-[length:var(--text-sm)] leading-relaxed whitespace-pre-wrap"
    >
      {data.markdown}
    </pre>
  );
}
