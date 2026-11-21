import { useItemMarkdown } from '../../feature/featureApi';
import { Markdown } from '../../../components/Markdown';
import { promptsTestIds as ids } from '../testIds';
import { hasAgentTagLines } from './source/highlight';

// The text as rendered Markdown (POST /api/markdown/render through the core client).
export function RenderedTab(props: { projectId: string; text: string }) {
  const rendered = useItemMarkdown(props.text, undefined, true);
  return (
    <section
      data-testid={ids.panelRendered}
      aria-label="Rendered"
      data-project={props.projectId}
      className="grid gap-3"
    >
      {hasAgentTagLines(props.text) ? (
        <p
          data-testid={ids.renderedSectionNote}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          Agent-specific sections show here as written. Each agent gets only its own.
        </p>
      ) : null}
      {rendered.isError ? (
        <p role="alert" data-testid={ids.renderedError} className="m-0 text-alert">
          The text could not be rendered.
        </p>
      ) : rendered.data ? (
        <div data-testid={ids.renderedBody}>
          <Markdown html={rendered.data.html} />
        </div>
      ) : (
        <p className="m-0 text-ink-muted">Rendering...</p>
      )}
    </section>
  );
}
