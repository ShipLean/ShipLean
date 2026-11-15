import type {
  FeatureTimelineViewDto,
  TimelineBarDto,
  TimelineEdgeDto,
  TimelineStoryDto,
} from '@shiplean/core/api/views';

export type RailItem =
  { kind: 'task'; bar: TimelineBarDto } | { kind: 'story'; story: TimelineStoryDto };

/** The bar, story or bug a key names in this view; null for a key the view does not hold. */
export function findRailItem(view: FeatureTimelineViewDto, key: string | null): RailItem | null {
  if (key === null) return null;
  const bar = view.bars.find((b) => b.key === key);
  if (bar) return { kind: 'task', bar };
  const story = view.stories.find((s) => s.key === key);
  return story ? { kind: 'story', story } : null;
}

export const edgesInto = (view: FeatureTimelineViewDto, key: string): TimelineEdgeDto[] =>
  view.edges.filter((e) => e.to === key);

export const edgesOutOf = (view: FeatureTimelineViewDto, key: string): TimelineEdgeDto[] =>
  view.edges.filter((e) => e.from === key);

/** The project key, from the page address the view carries (/p/<project>/items/<key>/timeline). */
export const projectKeyOf = (view: FeatureTimelineViewDto): string => view.links.ui.split('/')[2];
