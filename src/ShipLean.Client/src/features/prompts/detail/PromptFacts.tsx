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

// Inline code in a fact sentence (.facts code in the mock-up).
const codeClass =
  'rounded-[var(--radius-xs)] bg-sunken px-[3px] font-[family-name:var(--font-mono)] text-[length:0.92em]';

// The grey filled chips of ShipLean fills in and Inserted parts (.ph in the mock-up).
const chipClass =
  'mr-[3px] mb-[3px] inline-flex h-5 items-center rounded-[var(--radius-xs)] bg-sunken px-1.5 font-[family-name:var(--font-mono)] text-[length:var(--text-2xs)]';

/** A server facts string with each backtick span as inline code (D16). Nothing else is parsed. */
function FactText({ text }: { text: string }) {
  const parts = text.split('`');
  // An odd number of parts means every backtick is paired; an unpaired last one stays as text.
  const paired =
    parts.length % 2 === 1 ? parts : [...parts.slice(0, -2), parts.slice(-2).join('`')];
  return (
    <>
      {paired.map((part, i) =>
        i % 2 === 1 ? (
          <code key={i} data-testid={ids.factCode} className={codeClass}>
            {part}
          </code>
        ) : (
          part
        ),
      )}
    </>
  );
}

// When it runs, served by, read by, the placeholders ShipLean fills in, the inserted parts and the note. The strings are
// made by the server, with tool and part names in backticks.
export function PromptFacts({ prompt }: { prompt: PromptDto }) {
  return (
    <dl
      data-testid={ids.facts}
      aria-label={`Facts of ${prompt.name}`}
      className="m-0 grid grid-cols-[112px_minmax(0,1fr)] gap-x-3 gap-y-[5px] text-[length:var(--text-sm)] touch:grid-cols-1 touch:gap-y-0.5"
    >
      <Fact label="When it runs" testId={ids.factWhenRuns}>
        <FactText text={prompt.whenRuns} />
      </Fact>
      <Fact label="Served by" testId={ids.factServedBy}>
        <FactText text={prompt.servedBy} />
      </Fact>
      <Fact label="Read by" testId={ids.factReadBy}>
        <FactText text={prompt.readBy} />
      </Fact>
      {prompt.placeholders.length > 0 ? (
        <Fact label="ShipLean fills in" testId={ids.factPlaceholders}>
          {prompt.placeholders.map((placeholder) => (
            <code key={placeholder} data-testid={ids.placeholderChip} className={chipClass}>
              {placeholder}
            </code>
          ))}
        </Fact>
      ) : null}
      {prompt.insertedParts.length > 0 ? (
        <Fact label="Inserted parts" testId={ids.factInsertedParts}>
          {prompt.insertedParts.map((part) => (
            <code key={part} className={chipClass}>
              {part}
            </code>
          ))}
        </Fact>
      ) : null}
      {prompt.note === null ? null : (
        <Fact label="Note" testId={ids.factNote}>
          <FactText text={prompt.note} />
        </Fact>
      )}
    </dl>
  );
}
