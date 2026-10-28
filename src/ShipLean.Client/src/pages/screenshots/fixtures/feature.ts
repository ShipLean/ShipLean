import type { CommentDto } from '../../../api/comments';
import type { StateDto } from '../../../api/projects';
import type { WorkItemDto } from '../../../api/workItems';
import {
  feature as featureFixture,
  linkCards,
  markdownView,
  stubFeature,
} from '../../../features/feature/ct/stubFeature';
import { stubApi } from '../../../features/workItem/ct/stubApi';
import { attachment, change, revision, stubSide } from '../../../features/workItem/ct/stubSide';
import { stubComments } from '../../../features/workItem/ct/stubDetails';
import { PEOPLE, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS, stubShell } from '../shellStub';
import type { Page } from '../shoot';

// SHIP-100 Checkout v2 as design/screens/feature.html draws it, on the shell's people and sprints.
const figure =
  '<figure data-md-image="attached"><img src="/api/attachments/a-flow/content" alt="Checkout flow"><figcaption>checkout-flow.svg</figcaption></figure>';

export const checkoutV2: WorkItemDto = {
  ...featureFixture,
  assignee: PEOPLE.priya,
  createdBy: PEOPLE.ana,
  changedBy: PEOPLE.priya,
  description:
    'Rebuild checkout around **saved cards** and a single review step.\n\n![Checkout flow](checkout-flow.svg)',
  descriptionHtml: `<p>Rebuild checkout around <strong>saved cards</strong> and a single review step.</p>${figure}`,
  statusVerified: {
    at: '2026-10-25T09:30:00+00:00',
    atLabel: 'just now',
    stale: false,
    by: PEOPLE.priya,
    agent: 'Claude Code (claude.ai)',
    via: 'set_status',
  },
};

/** The same Feature with a status nobody has confirmed for three weeks. */
export const staleCheckoutV2: WorkItemDto = {
  ...checkoutV2,
  statusVerified: {
    at: '2026-10-04T09:30:00+00:00',
    atLabel: '3 weeks ago',
    stale: true,
    by: PEOPLE.priya,
    agent: 'Claude Code (claude.ai)',
    via: 'set_status',
  },
};

const stateRow = (
  id: string,
  name: string,
  order: number,
  category: StateDto['category'],
): StateDto => ({
  id,
  type: 'feature',
  name,
  category,
  order,
  itemCount: 0,
  startsAgent: false,
});
// Ids as the Feature fixture's state (…0202, Active) has them.
const featureStates: StateDto[] = [
  stateRow('s-idea', 'Idea', 1, 'proposed'),
  { ...stateRow(checkoutV2.state.id, 'Active', 2, 'inProgress') },
  stateRow('s-blocked', 'Blocked', 3, 'inProgress'),
  stateRow('s-done', 'Done', 4, 'completed'),
];

const logEntry = (
  id: string,
  author: CommentDto['author'],
  label: string,
  body: string,
  agentName: string | null = null,
): CommentDto => ({
  id,
  workItemId: checkoutV2.id,
  author,
  body,
  bodyHtml: `<p>${body}</p>`,
  createdAt: '2026-10-22T10:14:00+00:00',
  createdLabel: label,
  editedAt: null,
  editedLabel: null,
  agentName,
  superseded: null,
});

// Newest first is the screen's job; the API lists oldest first.
export const log: CommentDto[] = [
  logEntry('c1', PEOPLE.tom, 'Thu 22 Oct, 10:14', 'Vault rate limit is 50 a minute on staging.'),
  {
    ...logEntry('c2', PEOPLE.priya, 'Fri 23 Oct, 11:02', 'Estimate: two sprints.'),
    superseded: {
      at: '2026-10-24T09:00:00+00:00',
      atLabel: 'Sat 24 Oct, 09:00',
      by: PEOPLE.ana,
    },
  },
  logEntry(
    'c3',
    PEOPLE.priya,
    'Sat 24 Oct, 15:40',
    'Picker merged, vault wiring is next.',
    'Claude Code (claude.ai)',
  ),
];

const mdBy = (id: string, name: string, agent: string | null, replaced: number) => ({
  ...attachment(id, name, agent === null ? PEOPLE.mei : PEOPLE.priya, 14 * 1024),
  workItemId: checkoutV2.id,
  contentType: 'text/markdown',
  kind: 'markdown' as const,
  replacedCount: replaced,
  agentName: agent,
  via: agent === null ? null : 'attach_markdown',
});
export const attachments = [
  mdBy('a-design', 'design-v0.2.md', 'Claude Code (claude.ai)', 1),
  mdBy('a-rollout', 'rollout-plan.md', null, 0),
  {
    ...attachment('a-flow', 'checkout-flow.svg', PEOPLE.mei, 412 * 1024),
    workItemId: checkoutV2.id,
    contentType: 'image/svg+xml',
    kind: 'image' as const,
  },
  {
    ...attachment('a-pdf', 'vault-api-contract.pdf', PEOPLE.tom, 1.2 * 1024 * 1024),
    workItemId: checkoutV2.id,
    contentType: 'application/pdf',
  },
];

const revisions = [
  revision(6, PEOPLE.priya, [change('statusLine', 'Saved cards on staging.', 'Picker merged.')], {
    changedAtLabel: 'Sat 24 Oct, 15:40',
  }),
  revision(5, PEOPLE.ana, [change('goal', null, 'Returning customers pay in two taps.')], {
    changedAtLabel: 'Fri 16 Oct, 15:40',
  }),
];

const diagram =
  '<svg xmlns="http://www.w3.org/2000/svg" width="640" height="160"><rect width="640" height="160" fill="#e8eef7"/><rect x="24" y="48" width="160" height="64" rx="8" fill="#2f7fd8"/><rect x="240" y="48" width="160" height="64" rx="8" fill="#16a089"/><rect x="456" y="48" width="160" height="64" rx="8" fill="#7b4fc9"/></svg>';

// Cards as a server that read og:site_name sends them: the card's host is the site's own name ("GitHub", "Acme Wiki"),
// the host name only when the page had none (the second page card).
const siteNames: Record<string, string> = {
  'github.com': 'GitHub',
  'wiki.example.com': 'Acme Wiki',
};
const named = <T extends { host: string }>(card: T): T => ({
  ...card,
  host: siteNames[card.host] ?? card.host,
});
const siteLabelled = {
  ...linkCards,
  code: linkCards.code.map(named),
  pages: linkCards.pages.map(named),
};

/** Everything the Feature page and the attachment page read. The shell answers first; the Feature's stubs answer over it. */
export async function stubFeaturePage(page: Page, item: WorkItemDto, view?: typeof designView) {
  await stubShell(page);
  await stubApi(page, item);
  await stubFeature(page, { cards: siteLabelled, view });
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: featureStates }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/projects/by-key/SHIP', (route) =>
    route.fulfill({ json: SHELL_PROJECT }),
  );
  await page.route(
    /\/api\/(attachments\/[^/]+\/content|link-cards\/[^/]+\/(image|favicon))$/,
    (route) => route.fulfill({ status: 200, contentType: 'image/svg+xml', body: diagram }),
  );
  await stubComments(page, PEOPLE.priya, log);
  await stubSide(page, PEOPLE.priya, attachments, revisions);
}

/** The attachment page's Markdown file, on SHIP-100. */
export const designView = {
  ...markdownView,
  attachment: { ...attachments[0], replacedCount: 1 },
  markdown: '# Design\n\n## Flow\n\n![Checkout flow](checkout-flow.svg)\n',
  html: `<h1 id="design">Design</h1><h2 id="flow">Flow</h2>${figure}`,
  outline: [
    { level: 1, text: 'Design', anchor: 'design' },
    { level: 2, text: 'Flow', anchor: 'flow' },
  ],
  images: [{ src: 'checkout-flow.svg', status: 'attached' as const, attachmentId: 'a-flow' }],
  workItem: {
    ...markdownView.workItem,
    id: checkoutV2.id,
    key: checkoutV2.key,
    title: checkoutV2.title,
    type: checkoutV2.type,
  },
  siblings: [attachments[1]],
};
