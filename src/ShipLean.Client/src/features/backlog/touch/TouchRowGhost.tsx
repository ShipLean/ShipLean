import type { BacklogRowDto } from '@shiplean/core/api/views';

/** What follows the finger while a row is lifted by its handle: the key and title on one line (Planning and Tree). */
export function TouchRowGhost({ row }: { row: BacklogRowDto }) {
  return (
    <div className="w-[min(80vw,22rem)] truncate rounded-control border border-accent bg-panel px-3 py-3 text-[length:var(--text-sm)] font-medium shadow-lg">
      {row.key} {row.title}
    </div>
  );
}
