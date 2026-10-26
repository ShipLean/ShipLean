import { SWATCHES, type Swatch } from '../../../../api/persisted';
import { tagStyle } from '../../../board/cards/cardStyle';
import { taskboardSettingsTestIds as ids } from '../../testIds';

/** One tag, as a pill in its colour, with a swatch to pick the colour and a button to clear it. */
export function TagRow({
  tag,
  colour,
  readOnly,
  onChange,
}: {
  tag: string;
  colour: Swatch | null;
  readOnly: boolean;
  onChange(colour: Swatch | null): void;
}) {
  return (
    <div
      data-testid={ids.tagRow}
      className="flex items-center gap-3 border-b border-edge/60 py-1.5 last:border-b-0"
    >
      <span className="w-44 min-w-0">
        <span
          className="inline-flex h-4 items-center rounded-[var(--radius-xs)] px-1.5 text-[length:var(--text-2xs)] font-medium whitespace-nowrap text-ink"
          style={tagStyle(colour)}
        >
          {tag}
        </span>
      </span>
      <span role="group" aria-label={`Colour for ${tag}`} className="inline-flex gap-1">
        {SWATCHES.map((swatch) => (
          <button
            key={swatch}
            type="button"
            data-testid={ids.tagSwatch}
            aria-pressed={colour === swatch}
            aria-label={swatch}
            title={swatch}
            disabled={readOnly}
            className="size-5 rounded-full border-2 border-transparent aria-pressed:border-ink disabled:opacity-50"
            style={{ background: `var(--color-${swatch})` }}
            onClick={() => onChange(swatch)}
          />
        ))}
      </span>
      <span className="text-[length:var(--text-sm)] text-ink-muted">{colour ?? 'No colour'}</span>
      <button
        type="button"
        data-testid={ids.tagClear}
        aria-label={`Clear colour for ${tag}`}
        title="Clear colour"
        disabled={readOnly}
        className={`inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:opacity-35 ${colour === null ? 'invisible' : ''}`}
        onClick={() => onChange(null)}
      >
        ✕
      </button>
    </div>
  );
}
