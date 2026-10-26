import type { CommentDto } from '../../../api/comments';
import type { UserRef } from '../../../api/refs';
import type { WorkItemDto } from '../../../api/workItems';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { postDataJSON(): unknown; method(): string; url(): string };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});
const failure = (message: string, status = 400) =>
  json({ error: message, code: 'validation' }, status);

export const comment = (id: string, author: UserRef | null, body: string): CommentDto => ({
  id,
  workItemId: 'w141',
  author,
  body,
  bodyHtml: `<p>${body}</p>`,
  createdAt: '2026-10-20T09:00:00+00:00',
  createdLabel: '2 days ago',
  editedAt: null,
  editedLabel: null,
  agentName: null,
  superseded: null,
});

/** Stubs the comment endpoints and `GET /api/me` for a signed-in `me`. `failNext` makes the next write answer a 400. */
export async function stubComments(page: Page, me: UserRef, initial: CommentDto[]) {
  let comments = [...initial];
  const posted: string[] = [];
  const edited: { id: string; body: string }[] = [];
  const archived: string[] = [];
  const superseded: string[] = [];
  let failNext: string | null = null;
  const refuse = () => {
    const message = failNext;
    failNext = null;
    return message;
  };
  await page.route('**/api/me', (route) =>
    route.fulfill(
      json({
        ...me,
        email: 'me@example.com',
        isInstanceAdmin: false,
        status: 'active',
        hasProjects: true,
        canCreateProjects: true,
        theme: 'os',
        density: 'comfortable',
      }),
    ),
  );
  await page.route('**/api/work-items/*/comments', (route) => {
    if (route.request().method() === 'GET') return route.fulfill(json(comments));
    const { body } = route.request().postDataJSON() as { body: string };
    const refused = refuse();
    if (refused) return route.fulfill(failure(refused));
    posted.push(body);
    const created = {
      ...comment(`c${comments.length + 100}`, me, body),
      createdLabel: 'Just now',
    };
    comments = [...comments, created];
    return route.fulfill(json(created));
  });
  await page.route('**/api/comments/*', (route) => {
    const { body } = route.request().postDataJSON() as { body: string };
    const id = new URL(route.request().url()).pathname.split('/').pop() as string;
    const refused = refuse();
    if (refused) return route.fulfill(failure(refused));
    edited.push({ id, body });
    const changed = {
      ...(comments.find((c) => c.id === id) as CommentDto),
      body,
      bodyHtml: `<p>${body}</p>`,
      editedLabel: 'Just now',
    };
    comments = comments.map((c) => (c.id === id ? changed : c));
    return route.fulfill(json(changed));
  });
  await page.route('**/api/comments/*/supersede', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').slice(-2)[0] as string;
    const refused = refuse();
    if (refused) return route.fulfill(failure(refused));
    superseded.push(id);
    const changed = {
      ...(comments.find((c) => c.id === id) as CommentDto),
      superseded: { at: '2026-10-25T09:00:00+00:00', atLabel: 'Just now', by: me },
    };
    comments = comments.map((c) => (c.id === id ? changed : c));
    return route.fulfill(json(changed));
  });
  await page.route('**/api/comments/*/archive', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').slice(-2)[0] as string;
    const refused = refuse();
    if (refused) return route.fulfill(failure(refused));
    archived.push(id);
    comments = comments.filter((c) => c.id !== id);
    return route.fulfill({ status: 204 });
  });
  return {
    posted,
    edited,
    archived,
    superseded,
    failNext: (message: string) => {
      failNext = message;
    },
  };
}

/** Stubs `POST /api/work-items` for a new task: the parent the by-key read returns afterwards carries it as a child. */
export async function stubTaskCreate(
  page: Page,
  parent: WorkItemDto,
  setCurrent: (next: WorkItemDto) => void,
) {
  const created: unknown[] = [];
  let failNext: string | null = null;
  let current = parent;
  await page.route('**/api/work-items', (route) => {
    const create = route.request().postDataJSON() as { title: string };
    if (failNext) {
      const message = failNext;
      failNext = null;
      return route.fulfill(failure(message));
    }
    created.push(create);
    const number = 300 + created.length;
    current = {
      ...current,
      children: [
        ...current.children,
        {
          id: `t${number}`,
          key: `SHIP-${number}`,
          type: { id: 'task', name: 'Task' },
          title: create.title,
          state: { id: 's-new', name: 'New', category: 'proposed' },
          assignee: null,
          points: null,
        },
      ],
    };
    setCurrent(current);
    return route.fulfill(
      json({ id: `t${number}`, key: `SHIP-${number}`, revision: 1, changes: [], notices: [] }),
    );
  });
  return {
    created,
    failNext: (message: string) => {
      failNext = message;
    },
  };
}
