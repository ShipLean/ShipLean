import type { RetroCardDto } from '@shiplean/core/api/retros';
import { TypeIcon } from '../../../../../components/TypeIcon';
import { retrosTestIds as ids } from '../../../testIds';

/** The Feature retro callout above the columns: which feature retro the sprint has, who compiled it and from how many story retros. */
export function FeatureRetroCallout({ card }: { card: RetroCardDto }) {
  const { workItem, author, compiledFrom } = card;
  return (
    <aside
      role="note"
      aria-label="Feature retro"
      data-testid={ids.retroFeatureCallout}
      className="grid gap-1 rounded-md border border-edge bg-panel p-3 text-[length:var(--text-sm)] text-ink-muted"
    >
      <b className="inline-flex items-center gap-1.5 text-ink">
        Feature retro: <TypeIcon type="feature" />
        <span className="font-mono">{workItem?.key}</span> {workItem?.title}
      </b>
      <span>
        Compiled by {author.user.name}
        {author.agentName ? ` · ${author.agentName}` : ''} from {compiledFrom.length} story retros
        when the last story closed. Its points come first in each theme group.
      </span>
    </aside>
  );
}
