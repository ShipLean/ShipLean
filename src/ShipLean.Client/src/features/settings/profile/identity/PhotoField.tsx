import { useRef } from 'react';
import { settingsTestIds } from '../../testIds';
import { FormRow } from './FormRow';

export function PhotoField({
  hasPhoto,
  error,
  onPick,
  onRemove,
}: {
  hasPhoto: boolean;
  error: string | null;
  onPick(file: File): void;
  onRemove(): void;
}) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <FormRow label="Photo">
      <span className="flex items-center gap-2">
        <button
          type="button"
          data-testid={settingsTestIds.profilePhotoUpload}
          className="btn-ghost"
          onClick={() => input.current?.click()}
        >
          {hasPhoto ? 'Replace photo' : 'Upload photo'}
        </button>
        <input
          ref={input}
          type="file"
          hidden
          data-testid={settingsTestIds.profilePhotoInput}
          accept="image/png,image/jpeg,image/webp"
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = '';
            if (file !== undefined) onPick(file);
          }}
        />
        {hasPhoto ? (
          <button
            type="button"
            data-testid={settingsTestIds.profilePhotoRemove}
            className="btn-ghost border-transparent"
            onClick={onRemove}
          >
            Remove
          </button>
        ) : null}
      </span>
      {error === null ? null : (
        <span
          data-testid={settingsTestIds.profilePhotoError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </span>
      )}
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        Optional. PNG, JPEG or WebP up to 2 MB, cropped to a circle. It shows at 20px on cards, so a
        close-up of your face works best. Without one, your initials show on your colour.
      </span>
    </FormRow>
  );
}
