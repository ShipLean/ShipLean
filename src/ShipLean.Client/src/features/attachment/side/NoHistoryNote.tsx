import { attachmentTestIds as ids } from '../testIds';
import { PanelHeading } from './PanelHeading';

// Attachments have no versions: a same-name upload replaces the file.
export function NoHistoryNote({ name }: { name: string }) {
  return (
    <section data-testid={ids.noHistory} aria-label="Replacing this file" className="grid gap-1">
      <PanelHeading>Replacing this file</PanelHeading>
      <div className="rounded-control border border-edge bg-sunken p-2 text-[length:var(--text-sm)]">
        <b className="block">No version history for attachments</b>
        Uploading another <code>{name}</code> replaces this one, and the old copy is soft-deleted.
        Only the Feature&apos;s description keeps revisions.
      </div>
    </section>
  );
}
