import type { MeDto } from '../../../../api/me';
import { settingsTestIds } from '../../testIds';
import { FormRow } from './FormRow';
import { LookPreview } from './LookPreview';
import { AVATAR_COLOURS, hasPhoto, type IdentityDraft } from './model';

export function ColourField({
  me,
  draft,
  onPick,
}: {
  me: MeDto;
  draft: IdentityDraft;
  onPick(colour: string): void;
}) {
  return (
    <FormRow label="Colour">
      <div role="group" aria-label="Avatar colour" className="flex flex-wrap gap-1.5">
        {AVATAR_COLOURS.map((colour) => (
          <button
            key={colour}
            type="button"
            data-testid={settingsTestIds.profileColour}
            data-colour={colour}
            aria-pressed={draft.avatarColour === colour}
            aria-label={`Colour ${colour}`}
            className="size-6 cursor-pointer rounded-full border-2 border-transparent p-0 aria-pressed:border-ink"
            style={{ background: colour, boxShadow: 'inset 0 0 0 2px var(--color-panel)' }}
            onClick={() => onPick(colour)}
          />
        ))}
      </div>
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        {hasPhoto(me, draft)
          ? "Behind your photo while it loads, and wherever it can't be shown."
          : 'Behind your initials.'}
      </span>
      <LookPreview me={me} draft={draft} />
    </FormRow>
  );
}
