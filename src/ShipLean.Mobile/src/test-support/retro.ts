// Pure Retro fakes (no jest): random ids, shapes from the pinned retro fixtures, and a fake API for every Retro route.
import type { RetroActionDetailDto } from '@shiplean/core/api/retros';
import type {
  RetroActionDialogDto,
  RetroActionRefDto,
  RetroCategory,
  RetroFeatureRowDto,
  RetroFeaturesDto,
  RetroInsightsDto,
  RetroMergeProposalDto,
  RetroPointDto,
  RetroThemeRefDto,
  SprintRetroDto,
} from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import type { SprintRef, UserRef } from '@shiplean/core/api/refs';
import actionDetail from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-detail.json';
import features from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-features.json';
import insights from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-insights.json';
import retroPoint from '../../../ShipLean.TestSupport/DtoFixtures/retros/retro-point.json';
import sprintRetro from '../../../ShipLean.TestSupport/DtoFixtures/retros/sprint-retro.json';
import { aProject, aSprint, aStory, aWriteResult } from './builders';
import { controlledApi, type ControlledApi } from './controlledApi';
import type { ApiTable } from './fakeApi';

const uuid = (): string => globalThis.crypto.randomUUID();

export function aRetroPoint(overrides: Partial<RetroPointDto> = {}): RetroPointDto {
  return { ...(retroPoint as RetroPointDto), id: uuid(), ...overrides };
}

export function aSprintRetro(overrides: Partial<SprintRetroDto> = {}): SprintRetroDto {
  return { ...(sprintRetro as SprintRetroDto), ...overrides };
}

const user = (name: string, initials: string, colour: string): UserRef => ({
  id: uuid(),
  name,
  initials,
  avatarColour: colour,
  photoUrl: null,
});

const theme = (name: string, tone: RetroThemeRefDto['tone']): RetroThemeRefDto => ({
  id: uuid(),
  name,
  tone,
});

// Sprint 25, the design's retro: points by category and theme, one with supporters.
export function designRetro(): SprintRetroDto {
  const sprint: SprintRef = { id: uuid(), name: 'Sprint 25', status: 'current' };
  const priya = user('Priya Shah', 'PS', '#2f7fd8');
  const tom = user('Tom Reid', 'TR', '#d8742f');
  const builds = theme('Slow builds', 'bad');
  const contracts = theme('Contracts first worked', 'good');
  const baselines = theme('Screenshot baselines need CI', 'bad');

  const point = (
    category: RetroCategory,
    text: string,
    t: RetroThemeRefDto | null,
    overrides: Partial<RetroPointDto> = {},
  ) =>
    aRetroPoint({
      category,
      text,
      theme: t,
      sprint,
      action: null,
      decision: category === 'wentWell' ? null : 'undecided',
      notNeeded: null,
      supporters: [],
      supportedByMe: false,
      recurring: false,
      ...overrides,
    });

  const byCategory: Record<RetroCategory, RetroPointDto[]> = {
    wentWell: [
      point('wentWell', 'The contracts were on the branch before we started.', contracts),
      point('wentWell', 'Reviews came back within the hour.', null),
    ],
    notInPlan: [point('notInPlan', 'The signing key expired mid-sprint.', null)],
    wentBadly: [
      point('wentBadly', 'The S22 build took 40 minutes.', builds, {
        recurring: true,
        supporters: [priya, tom],
      }),
      point('wentBadly', 'Baselines had to be regenerated from CI twice.', baselines, {
        recurring: true,
      }),
      point('wentBadly', 'Two agents edited the same file.', null),
    ],
    improve: [
      point('improve', 'Profile the small model first.', null),
      point('improve', 'Say in the brief which baselines come from CI.', baselines),
    ],
  };

  return {
    ...aSprintRetro(),
    sprint,
    kpis: {
      withRetro: 4,
      finished: 6,
      recurringThemes: 2,
      actionItemsRaised: 0,
      pointsToDecide: 6,
    },
    categories: RETRO_CATEGORIES.map((category) => ({
      category,
      groups: byCategory[category].map((p) => ({
        theme: p.theme,
        recurring: p.recurring,
        points: [p],
      })),
      toDecide: byCategory[category].filter((p) => p.decision === 'undecided').length,
    })),
    cards: [],
    coverage: [],
    actionItems: [],
    recurring: [],
  };
}

export function designInsights(): RetroInsightsDto {
  const base = insights as RetroInsightsDto;
  const proposal: RetroMergeProposalDto = { ...base.mergeProposals[0], id: uuid() };
  const recurring = base.themes.filter((t) => t.status === 'recurring').slice(0, 3);
  const themes = [...recurring, ...base.themes.filter((t) => t.status !== 'recurring')].slice(0, 3);
  return {
    ...base,
    range: '6',
    sprints: base.sprints.slice(0, 6),
    columns: base.columns.slice(0, 6),
    themes: themes.map((t) => ({ ...t, counts: t.counts.slice(0, 6) })),
    mergeProposals: [proposal],
  };
}

export function designRetroFeatures(): RetroFeaturesDto {
  const base = (features as RetroFeaturesDto).features[0];
  const row = (n: number, title: string, overrides: Partial<RetroFeatureRowDto> = {}) => ({
    ...base,
    item: { ...base.item, id: uuid(), key: `SHIP-${n}`, title },
    ...overrides,
  });
  return {
    features: [
      row(101, 'Checkout v2'),
      row(102, 'Receipts', { noFeatureRetro: true, retros: 2, ownRetros: 0 }),
      row(103, 'Reporting'),
      row(104, 'Saved cards', { lastRetroLabel: 'Yesterday' }),
      row(108, 'Refunds', { noFeatureRetro: true, retros: 1, ownRetros: 0 }),
    ],
    loose: [],
  };
}

export interface RetroApiOptions {
  projectId?: string;
  retro?: SprintRetroDto;
  insights?: RetroInsightsDto;
  features?: RetroFeaturesDto;
  dialog?: RetroActionDialogDto;
  // The work item a new action creates; its key answers by-key and its id answers PATCH.
  newAction?: { workItemId: string; key: string };
}

const points = (retro: SprintRetroDto): RetroPointDto[] =>
  retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));

// A fake server for every Retro route, with the controls of controlledApi (failNext, offline, hang). `table` adds or
// replaces routes.
export function retroApi(table: ApiTable = {}, opts: RetroApiOptions = {}): ControlledApi {
  const projectId = opts.projectId ?? aProject().id;
  const retro = opts.retro ?? designRetro();
  const insightsDto = opts.insights ?? designInsights();
  const proposals = insightsDto.mergeProposals;
  const featuresDto = opts.features ?? designRetroFeatures();
  const all = points(retro);
  const openAction: RetroActionRefDto = {
    ...(actionDetail as RetroActionDetailDto).action,
    workItemId: opts.newAction?.workItemId ?? uuid(),
    key: opts.newAction?.key ?? 'SHIP-901',
  };
  const dialog: RetroActionDialogDto = opts.dialog ?? {
    selected: all.slice(0, 1),
    offered: all.slice(1),
    openActions: [openAction],
    suggestedTheme: null,
  };
  const detail: RetroActionDetailDto = {
    ...(actionDetail as RetroActionDetailDto),
    action: openAction,
    points: all.slice(0, 1),
  };
  const project = `/api/projects/${projectId}`;
  const routes: ApiTable = {
    [`GET ${project}/sprints`]: [aSprint({ id: retro.sprint.id, name: retro.sprint.name })],
    [`GET /api/sprints/${retro.sprint.id}/retro`]: retro,
    [`GET ${project}/retros/insights`]: insightsDto,
    [`GET ${project}/retros/features`]: featuresDto,
    [`GET ${project}/retros/action-dialog`]: dialog,
    [`GET ${project}/work-items/by-key/${openAction.key}`]: aStory({
      id: openAction.workItemId,
      key: openAction.key,
    }),
    [`POST ${project}/retros/points`]: (request) => ({
      status: 201,
      body: aRetroPoint({
        text: (request.body as { text?: string } | undefined)?.text ?? '',
        canArchive: true,
      }),
    }),
    [`POST ${project}/retros/actions`]: { status: 201, body: detail },
    [`POST /api/retros/actions/${openAction.workItemId}/points`]: detail,
    [`PATCH /api/work-items/${openAction.workItemId}`]: aWriteResult({
      id: openAction.workItemId,
      key: openAction.key,
    }),
  };
  for (const p of all) {
    const path = `/api/retros/points/${p.id}`;
    routes[`POST ${path}/support`] = { status: 204 };
    routes[`DELETE ${path}/support`] = { status: 204 };
    routes[`POST ${path}/archive`] = { status: 204 };
    routes[`POST ${path}/not-needed`] = { ...p, decision: 'notNeeded' };
    routes[`POST ${path}/undo-not-needed`] = { ...p, decision: 'undecided', notNeeded: null };
  }
  for (const proposal of proposals) {
    const path = `/api/retros/theme-merge-proposals/${proposal.id}`;
    routes[`POST ${path}/merge`] = { status: 204 };
    routes[`POST ${path}/keep-apart`] = { status: 204 };
  }
  return controlledApi({ ...routes, ...table });
}
