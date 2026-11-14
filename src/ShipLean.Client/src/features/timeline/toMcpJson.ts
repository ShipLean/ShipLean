import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';

// The fields C1 lists as enums: their camelCase values become snake_case in the MCP form (D17). Free text such as
// keyDates[].source is not an enum and stays as it is; feature.state is the workflow's state name, also free text.
const ENUM_PATHS = new Set([
  'keyDates.point',
  'gaps.from',
  'gaps.to',
  'bars.state',
  'bars.periods.state',
  'bars.reviews.verdict',
  'edges.kind',
  'edges.source',
]);

const snake = (name: string) => name.replace(/[A-Z]/g, (c) => `_${c.toLowerCase()}`);

const convert = (value: unknown, path: string): unknown => {
  if (Array.isArray(value)) return value.map((item) => convert(item, path));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([name, inner]) => [
        snake(name),
        convert(inner, path === '' ? name : `${path}.${name}`),
      ]),
    );
  }
  return typeof value === 'string' && ENUM_PATHS.has(path) ? snake(value) : value;
};

/** The view the way the feature_timeline tool sends it: snake_case names and snake_case enum values (D17). */
export function toMcpJson(view: FeatureTimelineViewDto): Record<string, unknown> {
  return convert(view, '') as Record<string, unknown>;
}
