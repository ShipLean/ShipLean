import { promptsTestIds as ids } from '../testIds';

// The text as rendered Markdown (POST /api/markdown/render through the core client).
export function RenderedTab(props: { projectId: string; text: string }) {
  // TODO: render props.text with the existing Markdown renderer
  return (
    <section data-testid={ids.panelRendered} aria-label="Rendered" data-project={props.projectId} />
  );
}
