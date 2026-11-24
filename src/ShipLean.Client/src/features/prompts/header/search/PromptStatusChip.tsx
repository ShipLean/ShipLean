import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../../testIds';

// A prompt's status as the results menu shows it (design prompts.html .chip): Built-in plain, Custom vN purple with a dot,
// Built-in updated amber with a dot, Using built-in muted.
export function PromptStatusChip({ prompt }: { prompt: PromptDto }) {
  const base =
    'inline-flex h-5 items-center gap-1 whitespace-nowrap rounded-full px-1.75 text-[length:var(--text-xs)] font-medium';
  switch (prompt.status) {
    case 'custom':
      return (
        <span data-testid={ids.searchResultStatus} className={`${base} bg-label-purple/[18%]`}>
          <span aria-hidden="true" className="size-1.5 rounded-full bg-label-purple" />
          Custom v{prompt.custom?.number}
        </span>
      );
    case 'builtInUpdated':
      return (
        <span data-testid={ids.searchResultStatus} className={`${base} bg-caution/[22%]`}>
          <span aria-hidden="true" className="size-1.5 rounded-full bg-caution" />
          Built-in updated
        </span>
      );
    case 'usingBuiltIn':
      return (
        <span data-testid={ids.searchResultStatus} className={`${base} bg-sunken text-ink-muted`}>
          Using built-in
        </span>
      );
    case 'builtIn':
      return (
        <span data-testid={ids.searchResultStatus} className={`${base} bg-sunken`}>
          Built-in
        </span>
      );
  }
}
