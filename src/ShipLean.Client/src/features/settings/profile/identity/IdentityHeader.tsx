import type { MeDto } from '../../../../api/me';
import { settingsTestIds } from '../../testIds';
import { BigAvatar } from './BigAvatar';
import { previewInitials, shownPhoto, type IdentityDraft } from './model';

export function IdentityHeader({ me, draft }: { me: MeDto; draft: IdentityDraft }) {
  return (
    <div className="flex items-center gap-3 border-b border-edge p-3">
      <BigAvatar
        initials={previewInitials(draft.name)}
        colour={draft.avatarColour}
        photoUrl={shownPhoto(me, draft)}
      />
      <span className="grid">
        <b data-testid={settingsTestIds.profileHeading} className="text-[length:var(--text-md)]">
          {draft.name.trim() === '' ? '—' : draft.name}
        </b>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          {me.email}
          {me.isInstanceAdmin ? ' · instance admin' : ''}
        </span>
      </span>
    </div>
  );
}
