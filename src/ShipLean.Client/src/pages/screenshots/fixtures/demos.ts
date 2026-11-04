import none from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import ready from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import unlisted from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-unlisted.json' with { type: 'json' };
import privateDemo from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-private.json' with { type: 'json' };
import youtubeFailed from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-failed.json' with { type: 'json' };
import working from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-working.json' with { type: 'json' };
import settingsDefault from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import settingsBranded from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings-branded.json' with { type: 'json' };
import type { ProjectDemoSettingsDto, SprintDemoDto, YouTubeProjectDto } from '../../../api/demos';
import type { SprintDto } from '../../../api/sprints';
import type { WorkItemDto } from '../../../api/workItems';
import { attachment, change, revision } from '../../../features/workItem/ct/stubSide';
import { PEOPLE } from '../shellStub';
import { sprintsWithGoals } from './sprints';
import type { Page } from '../shoot';
import { actionItem } from './workItem';

// The Demo tab and the Demos settings as the K4 and K6 rules allow them (demos.test.ts beside this file holds each
// state to those rules). The pinned JSON in TestSupport/DtoFixtures/demos is the base; a state the pins don't cover is
// derived from the nearest pin, changing only what its status changes.

/** The ended sprint the mockup draws (design/screens/sprint-demo.html): Sprint 23. */
export const ENDED_SPRINT = 'sprint-23';
/** The sprint that is still running, for Closable. */
export const OPEN_SPRINT = 'sprint-24';

const asDemo = (pin: unknown): SprintDemoDto => pin as SprintDemoDto;

const forSprint = (demo: SprintDemoDto, id: string, name: string): SprintDemoDto => ({
  ...demo,
  sprintId: id,
  sprintName: name,
});

// The pins are written for Sprint 24; the script names its sprint, so it moves with it.
const ended = (demo: SprintDemoDto): SprintDemoDto => ({
  ...forSprint(demo, ENDED_SPRINT, 'Sprint 23'),
  scriptHtml: demo.scriptHtml?.replaceAll('Sprint 24', 'Sprint 23') ?? null,
});

const noneDemo = ended(asDemo(none));
const readyDemo = ended(asDemo(ready));
const workingDemo = ended(asDemo(working));
const workingRun = workingDemo.run!;

/** Sprints 21 to 23 closed and decided, Sprint 24 running: the shell the Demo tab is shot in. */
export const demoSprints: SprintDto[] = sprintsWithGoals.map((sprint) =>
  sprint.status === 'past' ? { ...sprint, rolloverStatus: 'decided' } : sprint,
);

export const demoStates = {
  /** Sprint 24 is still open and has no demo: nothing to create yet. */
  closable: {
    ...forSprint(asDemo(none), OPEN_SPRINT, 'Sprint 24'),
    status: 'closable',
    statusMessage: 'Close the sprint to make its demo.',
    actions: { canCreate: false, createBlockedMessage: null, canRetryYouTube: false },
  },
  none: noneDemo,
  /** The automatic demo could not start: no BAU item was made, so no run. */
  blocked: {
    ...noneDemo,
    status: 'blocked',
    statusMessage: "The automatic demo didn't start: Storefront has no agent connection.",
    actions: {
      canCreate: false,
      createBlockedMessage: 'Connect an agent to this project, then create the demo here.',
      canRetryYouTube: false,
    },
  },
  /** Asked for, and no session has started yet. */
  requested: {
    ...workingDemo,
    status: 'requested',
    run: { ...workingRun, sessionState: null },
    actions: { canCreate: true, createBlockedMessage: null, canRetryYouTube: false },
  },
  working: workingDemo,
  /** The upload link expired with nothing delivered: the session has ended. */
  failed: {
    ...workingDemo,
    status: 'failed',
    statusMessage: 'The upload link expired before a video came back.',
    run: { ...workingRun, sessionState: 'ended' },
    actions: { canCreate: true, createBlockedMessage: null, canRetryYouTube: false },
  },
  ready: readyDemo,
  youtubeUnlisted: ended(asDemo(unlisted)),
  youtubePrivate: ended(asDemo(privateDemo)),
  youtubeFailed: ended(asDemo(youtubeFailed)),
} satisfies Record<string, SprintDemoDto>;

export async function stubDemo(page: Page, demo: SprintDemoDto) {
  await page.route('**/api/sprints/*/demo', (route) => route.fulfill({ json: demo }));
  // No video file is served: the element settles on its error state with its controls, the same in every run (an empty
  // 200 leaves the browser's loading spinner turning, which differs frame to frame).
  await page.route('**/api/attachments/*/content', (route) => route.fulfill({ status: 404 }));
  // The nocookie embed is a third-party page: a plain dark frame stands in for it.
  await page.route('https://www.youtube-nocookie.com/**', (route) =>
    route.fulfill({
      contentType: 'text/html',
      body: '<html><body style="margin:0;background:#101418"></body></html>',
    }),
  );
}

// ---- Demos settings (design/screens/settings-demos*.png) ----

const asSettings = (pin: unknown): ProjectDemoSettingsDto => pin as ProjectDemoSettingsDto;

const withYouTube = (youTube: YouTubeProjectDto): ProjectDemoSettingsDto => ({
  ...asSettings(settingsDefault),
  youTube,
});

const channel: YouTubeProjectDto = {
  status: 'pending',
  channelTitle: 'Storefront Official',
  visibility: null,
  checkedLabel: null,
  refusal: null,
};

export const settingsStates = {
  /** No YouTube client on the instance, ShipLean's own logo and tokens. */
  off: asSettings(settingsDefault),
  /** The project's own logo and tokens, and a connected channel. */
  branded: asSettings(settingsBranded),
  notConnected: withYouTube({ ...channel, status: 'notConnected', channelTitle: null }),
  pending: withYouTube(channel),
  refused: withYouTube({
    ...channel,
    refusal:
      "YouTube gave the test video private visibility, not the unlisted you chose. The channel or its account settings decide this. Change the channel's setting and check again, choose private, or don't use YouTube.",
  }),
} satisfies Record<string, ProjectDemoSettingsDto>;

export async function stubDemoSettings(page: Page, settings: ProjectDemoSettingsDto) {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await page.route('**/api/projects/*/demo-brand/logo?*', (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 32" width="640" height="128"><rect width="160" height="32" rx="6" fill="#c2410c"/><text x="80" y="21" text-anchor="middle" font-size="14" font-family="sans-serif" fill="#fff">Acme</text></svg>',
    }),
  );
}

// ---- The BAU item the demo runs as ----

/** SHIP-200, the BAU item `working` and `ready` point at: Business As Usual, on no sprint, board or parent. */
export const bauItem: WorkItemDto = {
  ...actionItem,
  id: 'w200',
  key: 'SHIP-200',
  number: 200,
  type: { id: 'bau', name: 'BAU' },
  title: 'Sprint 23 demo',
  description:
    'Make the demo video for Sprint 23: walk through what the sprint finished and upload it from the link in the session.',
  descriptionHtml:
    '<p>Make the demo video for Sprint 23: walk through what the sprint finished and upload it from the link in the session.</p>',
  state: { id: 's-bau-active', name: 'Active', category: 'inProgress' },
  assignee: null,
  createdBy: PEOPLE.priya,
  changedBy: PEOPLE.priya,
  priority: 3,
  points: null,
  createdAtLabel: 'Mon 2 Nov',
  changedAtLabel: 'Mon 2 Nov, 10:15',
};

/** What the agent uploaded to SHIP-200: the video, its chapters and its script. */
export const bauAttachments = [
  {
    ...attachment('a1', 'sprint-demo.mp4', PEOPLE.priya, 50841),
    workItemId: 'w200',
    createdLabel: 'Mon 2 Nov',
  },
  {
    ...attachment('a2', 'sprint-demo-chapters.json', PEOPLE.priya, 412),
    workItemId: 'w200',
    createdLabel: 'Mon 2 Nov',
  },
  {
    ...attachment('a3', 'sprint-demo-script.md', PEOPLE.priya, 1820),
    workItemId: 'w200',
    createdLabel: 'Mon 2 Nov',
  },
];

export const bauRevisions = [
  revision(2, PEOPLE.priya, [change('state', 's-bau-new', 's-bau-active', ['New', 'Active'])], {
    changedAtLabel: 'Mon 2 Nov, 10:15',
  }),
];
