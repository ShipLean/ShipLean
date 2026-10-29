import { Link } from 'react-router-dom';
import type { FeatureBoardSectionDto } from '../../api/views';
import { StateDot } from '../../components/StateDot';
import { TypeIcon } from '../../components/TypeIcon';
import { routePath } from '../../routes';
import { featureBoardTestIds as ids } from './testIds';

const percent = (closed: number, total: number) =>
  total === 0 ? 0 : Math.round((closed / total) * 100);

/** A Feature section's header: collapse, the Feature (or "Directly under" for the board's own stories), its counts and a link to open it. */
export function FeatureSectionHeader({
  projectKey,
  section,
  direct,
  collapsed,
  controls,
  onToggle,
}: {
  projectKey: string;
  section: FeatureBoardSectionDto;
  direct: boolean;
  collapsed: boolean;
  /** The id of the body the toggle shows and hides; none while the body is not drawn. */
  controls?: string;
  onToggle: () => void;
}) {
  const stories = percent(section.storiesClosed, section.storiesTotal);
  return (
    <div
      data-testid={ids.sectionHeader}
      data-depth={section.depth}
      className={`flex min-h-10 flex-wrap items-center gap-2 border-b border-edge py-1.5 pr-4 ${
        direct
          ? 'border-l-[3px] border-l-edge bg-panel'
          : 'border-l-[3px] border-l-[var(--type-feature)]'
      }`}
      style={{ paddingLeft: `calc(var(--space-3) + ${Math.max(section.depth - 1, 0) * 22}px)` }}
    >
      <button
        type="button"
        data-testid={ids.sectionToggle}
        aria-expanded={!collapsed}
        aria-controls={controls}
        aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${section.key}`}
        className="cursor-pointer border-0 bg-transparent px-1 text-ink-muted"
        onClick={onToggle}
      >
        {collapsed ? '▶' : '▼'}
      </button>
      {direct ? (
        <span data-testid={ids.sectionTitle} className="text-[length:var(--text-sm)] font-semibold">
          Directly under {section.key}
        </span>
      ) : (
        <>
          <TypeIcon type="feature" />
          <span data-testid={ids.sectionKey} className="text-[length:var(--text-sm)]">
            {section.key}
          </span>
          <span
            data-testid={ids.sectionTitle}
            className="text-[length:var(--text-md)] font-semibold"
          >
            {section.title}
          </span>
          {section.parentFeature ? (
            <span
              data-testid={ids.sectionIn}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              in {section.parentFeature.key} {section.parentFeature.title}
            </span>
          ) : null}
          <span data-testid={ids.sectionState}>
            <StateDot category={section.state.category} name={section.state.name} />
          </span>
        </>
      )}
      <span
        data-testid={ids.sectionStories}
        className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted"
      >
        {section.storiesClosed}/{section.storiesTotal} stories
        <span className="relative inline-block h-1 w-11 overflow-hidden rounded-[2px] bg-sunken">
          <i className="absolute inset-y-0 left-0 bg-good" style={{ width: `${stories}%` }} />
        </span>
      </span>
      <span data-testid={ids.sectionPoints} className="text-[length:var(--text-xs)] text-ink-muted">
        {section.pointsClosed ?? 0}/{section.pointsTotal ?? 0} pts
      </span>
      <span className="flex-1" />
      {direct ? null : (
        <Link
          data-testid={ids.sectionOpen}
          to={routePath('workItem', { key: projectKey, itemKey: section.key })}
          className="text-[length:var(--text-xs)] text-ink-muted no-underline hover:text-signal hover:underline"
        >
          Open Feature ↗
        </Link>
      )}
    </div>
  );
}
