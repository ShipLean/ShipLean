import { promptsTestIds as ids } from '../testIds';
import { MAX_PROMPT_BYTES } from './byteSize';
import { SideHeading } from './SideHeading';

const kb = (bytes: number) => (bytes / 1024).toFixed(1);

// The size against 64 KB (D10): warns from 90%, an error over the limit.
export function SizeMeter({ bytes }: { bytes: number }) {
  const percent = Math.min(100, Math.round((bytes / MAX_PROMPT_BYTES) * 100));
  const over = bytes > MAX_PROMPT_BYTES;
  const warn = !over && bytes >= MAX_PROMPT_BYTES * 0.9;
  const tone = over ? 'bg-alert' : warn ? 'bg-caution' : 'bg-signal';
  const textTone = over ? 'text-alert' : warn ? 'text-caution' : 'text-ink-muted';
  return (
    <section>
      <SideHeading>Size</SideHeading>
      <div
        data-testid={ids.editorSizeMeter}
        role="meter"
        aria-label="Prompt size"
        aria-valuemin={0}
        aria-valuemax={MAX_PROMPT_BYTES}
        aria-valuenow={bytes}
        data-state={over ? 'over' : warn ? 'warn' : 'ok'}
        className="h-1 overflow-hidden rounded-full bg-sunken"
      >
        <div className={`h-full ${tone}`} style={{ width: `${percent}%` }} />
      </div>
      <p
        data-testid={ids.editorSizeText}
        role={over ? 'alert' : undefined}
        className={`m-0 mt-1 ${textTone}`}
      >
        {kb(bytes)} KB of 64 KB{over ? ': too large to save' : ''}
      </p>
    </section>
  );
}
