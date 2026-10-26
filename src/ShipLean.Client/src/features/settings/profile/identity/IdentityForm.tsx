import { useEffect, useState } from 'react';
import type { MeDto } from '../../../../api/me';
import { useSignInMode } from '../../../auth/useSignInMode';
import { settingsTestIds } from '../../testIds';
import { ColourField } from './ColourField';
import { EmailRow } from './EmailRow';
import { IdentityHeader } from './IdentityHeader';
import { hasPhoto, isDirty, nameProblem, photoProblem, type IdentityDraft } from './model';
import { NameField } from './NameField';
import { PhotoField } from './PhotoField';
import { SaveBar } from './SaveBar';
import { useSaveIdentity } from './useSaveIdentity';

const fromMe = (me: MeDto): IdentityDraft => ({
  name: me.name,
  avatarColour: me.avatarColour,
  pickedFile: null,
  removePhoto: false,
});

/** The edits laid over the saved identity. Keyed on the user by its parent, so one person's edits never reach another. */
export function IdentityForm({ me }: { me: MeDto }) {
  const { proxy } = useSignInMode();
  const [edits, setDraft] = useState<IdentityDraft>(() => fromMe(me));
  // The provider owns the name: in proxy mode it is always the stored one, even after a rename elsewhere.
  const draft = proxy ? { ...edits, name: me.name } : edits;
  const [photoError, setPhotoError] = useState<string | null>(null);
  const picked = draft.pickedFile?.previewUrl;
  useEffect(() => (picked === undefined ? undefined : () => URL.revokeObjectURL(picked)), [picked]);

  const { save, saving, error, clearError } = useSaveIdentity(me, () => {
    setDraft((d) => ({ ...d, pickedFile: null, removePhoto: false }));
  });

  const pick = (file: File) => {
    const problem = photoProblem(file);
    setPhotoError(problem);
    if (problem === null) {
      setDraft((d) => ({
        ...d,
        pickedFile: { file, previewUrl: URL.createObjectURL(file) },
        removePhoto: false,
      }));
    }
  };

  return (
    <section
      aria-label="Profile"
      data-testid={settingsTestIds.profileIdentityPanel}
      className="rounded-panel border border-edge bg-panel"
    >
      <header className="flex min-h-11 items-center border-b border-edge px-3 py-2">
        <h2 className="text-[length:var(--text-md)] font-semibold text-ink">Profile</h2>
      </header>
      <IdentityHeader me={me} draft={draft} />
      <div className="grid grid-cols-[130px_minmax(0,1fr)] max-sm:grid-cols-1">
        <NameField
          name={draft.name}
          readOnly={proxy}
          onChange={(name) => {
            clearError();
            setDraft((d) => ({ ...d, name }));
          }}
        />
        <PhotoField
          hasPhoto={hasPhoto(me, draft)}
          error={photoError}
          onPick={pick}
          onRemove={() => {
            setPhotoError(null);
            setDraft((d) => ({ ...d, pickedFile: null, removePhoto: me.photoUrl !== null }));
          }}
        />
        <ColourField
          me={me}
          draft={draft}
          onPick={(avatarColour) => setDraft((d) => ({ ...d, avatarColour }))}
        />
        <EmailRow email={me.email} proxy={proxy} />
      </div>
      <SaveBar
        dirty={isDirty(me, draft)}
        canSave={nameProblem(draft.name) === null}
        saving={saving}
        error={error}
        onSave={() => save(draft)}
        onDiscard={() => {
          setPhotoError(null);
          clearError();
          setDraft(fromMe(me));
        }}
      />
    </section>
  );
}
