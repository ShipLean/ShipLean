import { useId } from 'react';
import { YOU_TUBE_VISIBILITIES, type YouTubeVisibility } from '@shiplean/core/api/demos';
import { demosTestIds as ids } from '../../../demos/testIds';

const labels: Record<YouTubeVisibility, string> = {
  public: 'Public',
  unlisted: 'Unlisted',
  private: 'Private',
};

const notes: Record<YouTubeVisibility, string> = {
  public: 'anyone can find it',
  unlisted: 'anyone with the link',
  private: 'only the channel owner',
};

/** The three visibilities as radios. Nothing is chosen until the person chooses. */
export function VisibilityChoice({
  value,
  disabled,
  onChange,
}: {
  value: YouTubeVisibility | null;
  disabled: boolean;
  onChange(visibility: YouTubeVisibility): void;
}) {
  const name = useId();
  return (
    <div
      data-testid={ids.youtubeVisibility}
      role="radiogroup"
      aria-label="Visibility"
      className="flex flex-wrap gap-x-4 gap-y-1"
    >
      {YOU_TUBE_VISIBILITIES.map((visibility) => (
        <span key={visibility} className="inline-flex items-center gap-1.5">
          <label className="inline-flex items-center gap-1.5 font-medium text-ink">
            <input
              type="radio"
              name={name}
              checked={value === visibility}
              disabled={disabled}
              onChange={() => onChange(visibility)}
            />
            {labels[visibility]}
          </label>
          <span className="text-ink-muted">({notes[visibility]})</span>
        </span>
      ))}
    </div>
  );
}
