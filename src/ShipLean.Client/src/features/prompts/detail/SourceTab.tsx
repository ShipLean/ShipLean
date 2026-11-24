import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { SourceBlock } from './source/SourceBlock';

// The Markdown source of the text in use, with headings, placeholders and agent tags highlighted.
export function SourceTab(props: { prompt: PromptDto }) {
  const { prompt } = props;
  const custom = prompt.custom?.inUse === true ? prompt.custom : null;
  const text = custom === null ? prompt.builtInText : custom.text;
  return (
    <section
      data-testid={ids.panelSource}
      aria-label={`Source of ${prompt.name}`}
      className="grid gap-3"
    >
      {prompt.sectionErrors > 0 ? (
        <p
          role="alert"
          data-testid={ids.sectionErrorFlag}
          className="m-0 rounded-control border border-caution px-3 py-2 text-[length:var(--text-sm)] text-caution"
        >
          This version has an agent-section error, so every agent gets only the untagged text. Edit
          it to fix the tags.
        </p>
      ) : null}
      <SourceBlock
        bleed
        text={text}
        label={custom === null ? 'Built-in prompt source' : `Custom v${custom.number} source`}
      />
    </section>
  );
}
