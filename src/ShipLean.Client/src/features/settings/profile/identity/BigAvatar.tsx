import { settingsTestIds } from '../../testIds';

/** The 48px avatar of the profile header: initials on the colour, or the photo over it (cropped to the circle). */
export function BigAvatar({
  initials,
  colour,
  photoUrl,
}: {
  initials: string;
  colour: string;
  photoUrl: string | null;
}) {
  return (
    <span
      data-testid={settingsTestIds.profileAvatar}
      data-has-photo={photoUrl !== null}
      className="relative inline-grid size-12 flex-none place-items-center overflow-hidden rounded-full text-[17px] leading-none font-semibold text-signal-ink"
      style={{ background: colour }}
    >
      {photoUrl === null ? initials : null}
      {photoUrl === null ? null : (
        <img
          data-testid={settingsTestIds.profileAvatarPhoto}
          src={photoUrl}
          alt=""
          className="absolute inset-0 size-full object-cover"
        />
      )}
    </span>
  );
}
