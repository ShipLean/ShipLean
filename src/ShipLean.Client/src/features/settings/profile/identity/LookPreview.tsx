import type { MeDto } from '../../../../api/me';
import { Avatar } from '../../../../components/Avatar';
import { settingsTestIds } from '../../testIds';
import { previewInitials, shownPhoto, type IdentityDraft } from './model';

/** How others see you, with the draft: on a card, and in the presence stack beside two others. */
export function LookPreview({ me, draft }: { me: MeDto; draft: IdentityDraft }) {
  const mine = {
    name: draft.name,
    initials: previewInitials(draft.name),
    avatarColour: draft.avatarColour,
    photoUrl: shownPhoto(me, draft),
  };
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-control border border-edge bg-canvas px-2.5 py-1.5 text-[length:var(--text-xs)] text-ink-muted">
      How others see you
      <span
        data-testid={settingsTestIds.profilePreviewCard}
        className="inline-flex items-center gap-1.5 rounded-control border border-edge bg-panel px-2 py-1 text-ink"
      >
        <span className="font-mono text-ink-muted">SHIP-141</span> Pay with saved card
        <Avatar {...mine} />
      </span>
      <span data-testid={settingsTestIds.profilePreviewPresence} className="inline-flex -space-x-1">
        <Avatar {...mine} />
        <Avatar initials="TO" avatarColour="#16a089" />
        <Avatar initials="ML" avatarColour="#7b4fc9" />
      </span>
    </div>
  );
}
