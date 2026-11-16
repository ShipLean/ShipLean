import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';

// The fields of InspectionMetricsViewDto that are C# enums: InspectionMetricsJson.McpOptions writes their values in
// snake_case (lastQuarter → last_quarter, lastCommit → last_commit). Free text such as leftOut[].reason stays as it is.
const ENUM_PATHS = new Set([
  'cadence',
  'range',
  'cut',
  'aggregate',
  'periods.dots.endedBy',
  'longest.endedBy',
  'longest.parentType',
  'periodDetail.tasks.endedBy',
  'periodDetail.tasks.parentType',
  'periodDetail.selected.task.endedBy',
  'periodDetail.selected.task.parentType',
  'periodDetail.selected.segments.kind',
  'periodDetail.selected.events.kind',
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

/** The view the way the inspection_metrics tool sends it: snake_case names and snake_case enum values (K4). */
export function toMcpJson(view: InspectionMetricsViewDto): Record<string, unknown> {
  return convert(view, '') as Record<string, unknown>;
}
