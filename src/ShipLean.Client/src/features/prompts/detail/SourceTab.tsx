import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The Markdown source of the text in use, with headings, placeholders and agent tags highlighted.
export function SourceTab(props: { prompt: PromptDto }) {
  // TODO: the highlighted source of the custom version in use, else the built-in text
  return <section data-testid={ids.panelSource} aria-label={`Source of ${props.prompt.name}`} />;
}
