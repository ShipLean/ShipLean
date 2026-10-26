import type { ImportMappingDto, JiraProjectDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';

// The five steps.
export const importSteps = [
  { label: 'Connection', next: 'Jira project' },
  { label: 'Jira project', next: 'Project key' },
  { label: 'Project key', next: 'Mapping' },
  { label: 'Mapping', next: 'Run' },
  { label: 'Run', next: '' },
] as const;
export const lastStep = importSteps.length;

// What step 3 makes of the picked Jira project and the project the import is opened in.
export type Target =
  // The project is empty (or this is a re-run into it): the import goes here.
  | { kind: 'ready'; rerun: boolean }
  // The project already has work items, and this Jira project was not imported into it.
  | { kind: 'nonEmpty' }
  // This Jira project was imported into another project; a re-run goes there.
  | { kind: 'offsite'; projectKey: string };

export function targetOf(project: ProjectDto, jira: JiraProjectDto): Target {
  if (jira.importedAs !== null) {
    return jira.importedAs.projectKey === project.key
      ? { kind: 'ready', rerun: true }
      : { kind: 'offsite', projectKey: jira.importedAs.projectKey };
  }
  // A project that never numbered an item has no work items.
  return project.nextItemNumber === 1 ? { kind: 'ready', rerun: false } : { kind: 'nonEmpty' };
}

// The mapping can be started from: something is imported, every status lands in a named state, and every person
// matched to a ShipLean user names one.
export function mappingValid(mapping: ImportMappingDto): boolean {
  const imported = new Set(
    mapping.issueTypes.filter((t) => t.target !== 'skip').map((t) => t.jiraType),
  );
  return (
    imported.size > 0 &&
    mapping.statuses
      .filter((s) => imported.has(s.jiraType))
      .every((s) => s.targetState.trim() !== '') &&
    mapping.users.every(
      (u) => (u.resolution !== 'matched' && u.resolution !== 'existing') || u.user !== null,
    )
  );
}

// Decision 8: a re-run with nothing new since the last run does not ask again.
export function isNewRow(mapping: ImportMappingDto): boolean {
  return (
    mapping.issueTypes.some((t) => t.isNewSinceLastRun) ||
    mapping.statuses.some((s) => s.isNewSinceLastRun) ||
    mapping.users.some((u) => u.isNewSinceLastRun)
  );
}

export const mappingSkipped = (target: Target | null, mapping: ImportMappingDto | null) =>
  target?.kind === 'ready' && target.rerun && mapping !== null && !isNewRow(mapping);

export interface StepInputs {
  baseUrl: string | null;
  tested: boolean;
  jira: JiraProjectDto | null;
  target: Target | null;
  projectKey: string;
  // null until the suggestion has been read.
  mapping: ImportMappingDto | null;
  // The user changed the mapping in this visit: a re-run sends it even when nothing was new.
  edited: boolean;
}

export interface StepState {
  // Whether Next is on at each step (index 0 is step 1).
  canNext: boolean[];
  // The furthest step the answers so far allow.
  reachable: number;
  summaries: string[];
  hints: string[];
  skipped: boolean;
}

export function describeSteps(i: StepInputs): StepState {
  const ready = i.jira !== null && i.target?.kind === 'ready';
  const skipped = !i.edited && mappingSkipped(i.target, i.mapping);
  const mappingOk = ready && i.mapping !== null && mappingValid(i.mapping);
  const canNext = [i.tested, i.jira !== null, ready, mappingOk, false];
  const reachable = mappingOk ? 5 : ready ? 4 : i.jira !== null ? 3 : i.tested ? 2 : 1;
  const mappingSummary =
    i.mapping === null
      ? 'Not started'
      : skipped
        ? 'Same as last run'
        : mappingOk
          ? `${i.mapping.totals.issues.toLocaleString('en-GB')} issues`
          : 'Needs attention';
  return {
    canNext,
    reachable,
    skipped,
    summaries: [
      i.baseUrl?.replace(/^https?:\/\//, '') ?? 'Not connected',
      i.jira === null
        ? 'Not picked'
        : `${i.jira.key} · ${i.jira.issueCount.toLocaleString('en-GB')} issues`,
      ready ? `Into ${i.projectKey}` : 'Not set',
      mappingSummary,
      'Not started',
    ],
    hints: [
      i.tested ? 'Connection works.' : 'Test the connection to continue.',
      i.jira === null
        ? 'Pick a Jira project to continue.'
        : `${i.jira.key} · ${i.jira.name} selected.`,
      ready ? `Imports into ${i.projectKey}.` : `${i.projectKey} cannot be the target.`,
      mappingOk
        ? 'Warnings do not block the run.'
        : 'Import at least one type, and name a state and a user wherever one is needed.',
      '',
    ],
  };
}

// Where Next and Back go from `step`: a skipped mapping is stepped over both ways.
export const nextStep = (step: number, skipped: boolean) => (step === 3 && skipped ? 5 : step + 1);
export const backStep = (step: number, skipped: boolean) => (step === 5 && skipped ? 3 : step - 1);
export const nextLabel = (step: number, skipped: boolean) =>
  importSteps[nextStep(step, skipped) - 1]?.label ?? '';

// What the run would create that ShipLean does not have yet.
export const newUserCount = (mapping: ImportMappingDto) =>
  mapping.users.filter((u) => u.resolution === 'create').length;

export function newStateCount(mapping: ImportMappingDto): number {
  const imported = new Set(
    mapping.issueTypes.filter((t) => t.target !== 'skip').map((t) => t.jiraType),
  );
  return new Set(
    mapping.statuses
      .filter((s) => s.createsState && imported.has(s.jiraType))
      .map((s) => s.targetState),
  ).size;
}
