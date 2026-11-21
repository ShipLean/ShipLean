import type { ReactNode } from 'react';
import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

function Fact({ label, testId, children }: { label: string; testId: string; children: ReactNode }) {
  return (
    <>
      <dt className="pt-px text-[length:var(--text-xs)] font-medium text-ink-muted">{label}</dt>
      <dd data-testid={testId} className="m-0 min-w-0 break-words">
        {children}
      </dd>
    </>
  );
}

const codeClass =
  'mr-1 inline-block rounded-[3px] bg-sunken px-[3px] font-mono text-[length:0.92em]';

// When it runs, served by, read by, the placeholders ShipLean fills in, the inserted parts and the note. The strings are
// plain text made by the server.
export function PromptFacts({ prompt }: { prompt: PromptDto }) {
  return (
    <dl
      data-testid={ids.facts}
      aria-label={`Facts of ${prompt.name}`}
      className="m-0 grid grid-cols-[112px_minmax(0,1fr)] gap-x-3 gap-y-[5px] text-[length:var(--text-sm)] touch:grid-cols-1 touch:gap-y-0.5"
    >
      <Fact label="When it runs" testId={ids.factWhenRuns}>
        {prompt.whenRuns}
      </Fact>
      <Fact label="Served by" testId={ids.factServedBy}>
        {prompt.servedBy}
      </Fact>
      <Fact label="Read by" testId={ids.factReadBy}>
        {prompt.readBy}
      </Fact>
      {prompt.placeholders.length > 0 ? (
        <Fact label="ShipLean fills in" testId={ids.factPlaceholders}>
          {prompt.placeholders.map((placeholder) => (
            <code key={placeholder} data-testid={ids.placeholderChip} className={codeClass}>
              {placeholder}
            </code>
          ))}
        </Fact>
      ) : null}
      {prompt.insertedParts.length > 0 ? (
        <Fact label="Inserted parts" testId={ids.factInsertedParts}>
          {prompt.insertedParts.map((part) => (
            <code key={part} className={codeClass}>
              {part}
            </code>
          ))}
        </Fact>
      ) : null}
      {prompt.note === null ? null : (
        <Fact label="Note" testId={ids.factNote}>
          {prompt.note}
        </Fact>
      )}
    </dl>
  );
}
