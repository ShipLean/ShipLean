import featureFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.feature.json' with { type: 'json' };
import linkCardsFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/links/link-cards.json' with { type: 'json' };
import markdownViewFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/attachments/attachment-markdown.json' with { type: 'json' };
import renderedFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/markdown/rendered-images.json' with { type: 'json' };
import type { ApiError } from '../../../api/client';
import type {
  AttachmentCheckDto,
  AttachmentCheckRequest,
  AttachmentMarkdownDto,
  AttachmentOutcome,
} from '../../../api/attachments';
import type {
  LinkCardDto,
  LinkCardsDto,
  LinkPreviewDto,
  WorkItemLinkRowDto,
} from '../../../api/linkCards';
import type { RenderedMarkdownDto } from '../../../api/markdown';
import type { WorkItemDto } from '../../../api/workItems';
import type { ProjectDto } from '../../../api/projects';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; url(): string; postDataJSON(): unknown };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

// The project WorkItemFormHarness mounts (p1, SHIP), as GET api/projects/by-key/SHIP answers it. A spec can't import it
// from the harness: the spec runs in Node, and the harness is a component module.
const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 2,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 200,
};

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** The Feature from work-item.feature.json (SHIP-100 Checkout v2), in the CT project (p1) the form stubs answer for. */
export const feature: WorkItemDto = { ...(featureFixture as WorkItemDto), projectId: project.id };

export const linkCards = linkCardsFixture.linkCards as LinkCardsDto;
export const linkPreview = linkCardsFixture.preview as LinkPreviewDto;
export const privateLinkPreview = linkCardsFixture.privatePreview as LinkPreviewDto;
export const markdownView = markdownViewFixture.markdownView as AttachmentMarkdownDto;
export const renderedWithImages = renderedFixture.rendered as RenderedMarkdownDto;

/** The calls stubFeature answers; `failNext` names one of them. */
export type FeatureCall =
  | 'linkCards'
  | 'preview'
  | 'addLinkCard'
  | 'addWorkItemLink'
  | 'editLinkCard'
  | 'archiveLinkCard'
  | 'refresh'
  | 'check'
  | 'markdownView'
  | 'render';

export interface FeatureStubOptions {
  /** What GET link-cards answers first; adds, edits and archives change it from there. */
  cards?: LinkCardsDto;
  /** What the preview answers. */
  preview?: LinkPreviewDto;
  /** The outcome the upload check gives each file, by name (the rest are `add`). */
  outcomes?: Record<string, { outcome: AttachmentOutcome; message: string }>;
  /** What GET api/attachments/{id}/markdown answers. */
  view?: AttachmentMarkdownDto;
}

/**
 * Stubs the Feature form's own endpoints (C3 link cards, C6 check and Markdown view, C7 render) plus the project by
 * key the regions resolve. Use it with stubApi (the item, states, members, sprints), stubSide (attachments, history)
 * and stubComments (the Log). `calls` records each request as `{ call, body }`; `failNext(call, status, error)` makes
 * the next request of that call answer with that error body (`field` included when given).
 */
export async function stubFeature(page: Page, options: FeatureStubOptions = {}) {
  let cards: LinkCardsDto = structuredClone(options.cards ?? linkCards);
  const calls: { call: FeatureCall; body: unknown }[] = [];
  const failures = new Map<FeatureCall, { status: number; error: ApiError }>();

  // Records the call; answers its queued failure if there is one, otherwise `answer()`.
  const handle =
    (call: FeatureCall, answer: (route: StubRoute) => { status: number; body?: unknown }) =>
    (route: StubRoute) => {
      const method = route.request().method();
      calls.push({ call, body: method === 'GET' ? null : route.request().postDataJSON() });
      const failure = failures.get(call);
      if (failure) {
        failures.delete(call);
        return route.fulfill(json(failure.error, failure.status));
      }
      const { status, body } = answer(route);
      return route.fulfill(body === undefined ? { status } : json(body, status));
    };
  const idOf = (route: StubRoute, fromEnd: number) =>
    new URL(route.request().url()).pathname.split('/').slice(-fromEnd)[0] as string;

  await page.route('**/api/projects/by-key/SHIP', (route) => route.fulfill(json(project)));
  await page.route('**/api/work-items/*/link-cards', (route) =>
    route.request().method() === 'GET'
      ? handle('linkCards', () => ({ status: 200, body: cards }))(route)
      : handle('addLinkCard', (r) => {
          const request = r.request().postDataJSON() as {
            url: string;
            title?: string;
            note?: string;
          };
          const added: LinkCardDto = {
            ...(cards.pages[0] ?? linkCards.pages[0]),
            id: `card-${calls.length}`,
            url: request.url,
            title: request.title ?? request.url,
            titleSource: request.title ? 'user' : 'url',
            note: request.note ?? null,
          };
          cards = { ...cards, pages: [...cards.pages, added] };
          return { status: 201, body: added };
        })(route),
  );
  await page.route(
    '**/api/work-items/*/link-cards/preview',
    handle('preview', () => ({ status: 200, body: options.preview ?? linkPreview })),
  );
  await page.route(
    '**/api/work-items/*/link-cards/refresh',
    handle('refresh', () => ({ status: 200, body: cards })),
  );
  await page.route(
    '**/api/work-items/*/work-item-links',
    handle('addWorkItemLink', (r) => {
      const request = r.request().postDataJSON() as {
        targetKey: string;
        type: WorkItemLinkRowDto['type'];
      };
      const row: WorkItemLinkRowDto = {
        ...linkCards.workItems[0],
        id: `row-${calls.length}`,
        key: request.targetKey,
        type: request.type,
      };
      cards = { ...cards, workItems: [...cards.workItems, row] };
      return { status: 201, body: row };
    }),
  );
  await page.route(
    '**/api/link-cards/*',
    handle('editLinkCard', (r) => {
      const id = idOf(r, 1);
      const edit = r.request().postDataJSON() as { title?: string; note?: string };
      const card = [...cards.code, ...cards.pages].find((c) => c.id === id);
      if (!card) return { status: 404, body: { error: 'Link not found.', code: 'not_found' } };
      const edited: LinkCardDto = {
        ...card,
        ...(edit.title === undefined ? {} : { title: edit.title, titleSource: 'user' as const }),
        ...(edit.note === undefined ? {} : { note: edit.note === '' ? null : edit.note }),
      };
      const swap = (list: LinkCardDto[]) => list.map((c) => (c.id === id ? edited : c));
      cards = { ...cards, code: swap(cards.code), pages: swap(cards.pages) };
      return { status: 200, body: edited };
    }),
  );
  await page.route(
    '**/api/link-cards/*/archive',
    handle('archiveLinkCard', (r) => {
      const id = idOf(r, 2);
      cards = {
        workItems: cards.workItems.filter((row) => row.id !== id),
        code: cards.code.filter((c) => c.id !== id),
        pages: cards.pages.filter((c) => c.id !== id),
      };
      return { status: 204 };
    }),
  );
  await page.route(
    '**/api/work-items/*/attachments/check',
    handle('check', (r) => {
      const request = r.request().postDataJSON() as AttachmentCheckRequest;
      const answer: AttachmentCheckDto = {
        files: request.files.map((file) => ({
          name: file.name,
          ...(options.outcomes?.[file.name] ?? { outcome: 'add' as const, message: '' }),
        })),
      };
      return { status: 200, body: answer };
    }),
  );
  await page.route(
    '**/api/attachments/*/markdown',
    handle('markdownView', () => ({ status: 200, body: options.view ?? markdownView })),
  );
  await page.route(
    '**/api/markdown/render',
    handle('render', (r) => {
      const { markdown } = r.request().postDataJSON() as { markdown: string };
      const answer: RenderedMarkdownDto = {
        markdown,
        html: `<p>rendered:${markdown}</p>`,
        images: [],
      };
      return { status: 200, body: answer };
    }),
  );

  return {
    calls,
    failNext: (call: FeatureCall, status: number, error: ApiError) => {
      failures.set(call, { status, error });
    },
  };
}
