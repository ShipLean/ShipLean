import type { AttachmentDto } from '../../../api/attachments';
import type { FieldChange } from '../../../api/persisted';
import type { UserRef } from '../../../api/refs';
import type { RevisionDto } from '../../../api/workItems';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; url(): string; postData(): string | null };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

export const attachment = (
  id: string,
  name: string,
  by: UserRef | null,
  sizeBytes = 2048,
): AttachmentDto => ({
  id,
  workItemId: 'w141',
  name,
  contentType: 'application/octet-stream',
  sizeBytes,
  uploadedBy: by,
  createdAt: '2026-10-20T09:00:00+00:00',
  createdLabel: '2 days ago',
  downloadUrl: `/api/attachments/${id}/content`,
  kind: 'file',
  replacedCount: 0,
  agentName: null,
  via: null,
});

export const revision = (
  rev: number,
  changedBy: UserRef | null,
  changes: FieldChange[],
  extra: Partial<RevisionDto> = {},
): RevisionDto => ({
  rev,
  changedBy,
  changedAt: '2026-10-20T09:00:00+00:00',
  changedAtLabel: '2 days ago',
  origin: 'user',
  changes,
  agentName: null,
  webhook: null,
  ...extra,
});

export const change = (
  field: string,
  from: string | number | null,
  to: string | number | null,
  labels: [string, string] | null = [String(from), String(to)],
): FieldChange => ({
  field,
  from,
  to,
  fromLabel: labels ? labels[0] : null,
  toLabel: labels ? labels[1] : null,
});

/** Stubs the attachment and history endpoints. `refuseNext` makes the next upload answer a 400 with that message. */
export async function stubSide(
  page: Page,
  me: UserRef,
  initial: AttachmentDto[],
  revisions: RevisionDto[],
) {
  let attachments = [...initial];
  const uploaded: string[] = [];
  const archived: string[] = [];
  let refuse: string | null = null;
  await page.route('**/api/work-items/*/attachments', (route) => {
    if (route.request().method() === 'GET') return route.fulfill(json(attachments));
    if (refuse) {
      const message = refuse;
      refuse = null;
      return route.fulfill(json({ error: message, code: 'validation' }, 400));
    }
    const name = /filename="([^"]+)"/.exec(route.request().postData() ?? '')?.[1] ?? '';
    uploaded.push(name);
    const created = {
      ...attachment(`a${attachments.length + 100}`, name, me),
      createdLabel: 'Just now',
    };
    attachments = [...attachments, created];
    return route.fulfill(json(created));
  });
  await page.route('**/api/attachments/*/archive', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').slice(-2)[0] as string;
    archived.push(id);
    attachments = attachments.filter((a) => a.id !== id);
    return route.fulfill({ status: 204 });
  });
  await page.route('**/api/work-items/*/history', (route) => route.fulfill(json({ revisions })));
  return {
    uploaded,
    archived,
    refuseNext: (message: string) => {
      refuse = message;
    },
  };
}
