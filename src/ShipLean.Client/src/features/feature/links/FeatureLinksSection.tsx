import type { FeatureRegionProps } from '../FeatureRegionProps';
import { useLinkCards } from '../featureApi';
import { featureTestIds as ids } from '../testIds';
import { AddLink } from './add/AddLink';
import { LinksList } from './section/LinksList';
import { useRefreshDue } from './section/useRefreshDue';

// The Feature's links: work items as rows, URL cards grouped Code and Pages. They save straight away, never through the form.
export function FeatureLinksSection(props: FeatureRegionProps) {
  const { item, projectKey } = props;
  const links = useLinkCards(item.projectId, item.key, item.id);
  useRefreshDue(item.projectId, item.key, item.id, links.data);
  const count = links.data
    ? links.data.workItems.length + links.data.code.length + links.data.pages.length
    : null;
  return (
    <section data-testid={ids.links} aria-label="Links" className="grid gap-2">
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">Links</h2>
        {count === null ? null : (
          <span
            data-testid={ids.linksCount}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            {count}
          </span>
        )}
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Work items and URLs. Code links show live state. Links save straight away.
        </span>
      </header>
      <AddLink {...props} />
      {links.isError ? (
        <div
          data-testid={ids.linksLoadFailed}
          role="alert"
          className="flex items-center gap-2 text-[length:var(--text-sm)] text-danger"
        >
          Links could not be loaded.
          <button
            type="button"
            data-testid={ids.linksRetry}
            className="h-7 rounded-control border border-edge px-2 text-ink"
            onClick={() => void links.refetch()}
          >
            Retry
          </button>
        </div>
      ) : null}
      {links.data ? (
        <LinksList
          cards={links.data}
          projectKey={projectKey}
          projectId={item.projectId}
          itemKey={item.key}
        />
      ) : null}
    </section>
  );
}
