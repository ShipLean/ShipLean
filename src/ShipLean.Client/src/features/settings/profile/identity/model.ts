import type { MeDto } from '../../../../api/me';

/** The identity colours (design/screens/profile.html); the server accepts only these. */
export const AVATAR_COLOURS = [
  '#2f7fd8',
  '#16a089',
  '#7b4fc9',
  '#c9611a',
  '#b83d7a',
  '#3f7d5a',
  '#5a5fc9',
  '#8a5a2b',
  '#a3416b',
  '#52627c',
] as const;

export const MAX_PHOTO_BYTES = 2 * 1024 * 1024;
export const PHOTO_TYPES = ['image/png', 'image/jpeg', 'image/webp'] as const;
export const MAX_NAME_LENGTH = 100;

/** What the form holds: the saved values with the edits laid over them. `photo` is what the avatar shows. */
export interface IdentityDraft {
  name: string;
  avatarColour: string;
  /** A file picked and not yet saved, with the object URL its preview shows. */
  pickedFile: { file: File; previewUrl: string } | null;
  /** The saved photo is to be removed on Save. */
  removePhoto: boolean;
}

export const draftOf = (): IdentityDraft => ({
  name: '',
  avatarColour: '',
  pickedFile: null,
  removePhoto: false,
});

/** The preview's initials: the first letters of the first and last words, as the server derives them on save. */
export function previewInitials(name: string): string {
  const letters = name
    .split(/\s+/)
    .map((word) => word.normalize('NFD')[0] ?? '')
    .filter((c) => /^[A-Za-z]$/.test(c));
  if (letters.length === 0) return 'U';
  if (letters.length === 1) return letters[0].toUpperCase();
  return `${letters[0]}${letters[letters.length - 1]}`.toUpperCase();
}

/** The photo the avatar shows for this draft: the picked file, nothing when removed, else the saved one. */
export function shownPhoto(me: MeDto, draft: IdentityDraft): string | null {
  if (draft.pickedFile !== null) return draft.pickedFile.previewUrl;
  return draft.removePhoto ? null : me.photoUrl;
}

export const hasPhoto = (me: MeDto, draft: IdentityDraft) => shownPhoto(me, draft) !== null;

/** A message when the name can't be saved, else null. Control characters are the server's rule too. */
export function nameProblem(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed === '') return 'Enter a name';
  if (trimmed.length > MAX_NAME_LENGTH)
    return `The name can have at most ${MAX_NAME_LENGTH} characters`;
  const hasControl = Array.from(trimmed).some((c) => {
    const code = c.codePointAt(0) ?? 0;
    return code < 0x20 || (code >= 0x7f && code <= 0x9f);
  });
  return hasControl ? 'The name cannot contain control characters' : null;
}

/** A message when the file can't be uploaded, else null. The server checks the bytes; this saves the round trip. */
export function photoProblem(file: { size: number; type: string }): string | null {
  if (!(PHOTO_TYPES as readonly string[]).includes(file.type))
    return 'A photo must be a PNG, JPEG or WebP image';
  return file.size > MAX_PHOTO_BYTES ? 'That photo is over 2 MB. Pick a smaller one' : null;
}

export const isDirty = (me: MeDto, draft: IdentityDraft) =>
  draft.name.trim() !== me.name ||
  draft.avatarColour !== me.avatarColour ||
  draft.pickedFile !== null ||
  draft.removePhoto;
