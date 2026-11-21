import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// When it runs, served by, read by, the placeholders ShipLean fills in, the inserted parts and the note.
export function PromptFacts(props: { prompt: PromptDto }) {
  // TODO: the facts rows from the prompt
  return <dl data-testid={ids.facts} aria-label={`Facts of ${props.prompt.name}`} hidden />;
}
