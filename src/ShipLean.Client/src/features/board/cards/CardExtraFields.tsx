import type { CardFieldDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';

// The extra fields the board's card settings add; an empty one reads as a dash (the server only sends it when the
// settings ask for empty fields).
export function CardExtraFields({ fields }: { fields: CardFieldDto[] }) {
  if (fields.length === 0) return null;
  return (
    <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-2 text-[length:var(--text-xs)] text-ink-muted">
      {fields.map((field) => (
        <div
          key={field.fieldId}
          data-testid={ids.cardExtra}
          data-field={field.fieldId}
          className="contents"
        >
          <dt>{field.label}</dt>
          <dd className="m-0 truncate text-ink">{field.text ?? '—'}</dd>
        </div>
      ))}
    </dl>
  );
}
