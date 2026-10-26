import type { ChecklistItem, LinkType, LinkValue } from '../../../api/persisted';
import type { SprintRef, StateRef, UserRef } from '../../../api/refs';
import type {
  AutoLinkDto,
  LinkCardDto,
  LinkDto,
  ParentRefDto,
  WorkItemChanges,
  WorkItemDto,
} from '../../../api/workItems';

/** A link in the draft. A saved link has its row id; one added in the form has `id: null` until a save. `uid` tells draft entries apart. */
export interface DraftLink {
  uid: string;
  id: string | null;
  type: LinkType;
  workItemId: string | null;
  key: string | null;
  title: string | null;
  url: string | null;
  /** What a webhook reported about the pull or merge request behind a saved url link; the server's, never edited. */
  card: LinkCardDto | null;
  autoLinked: AutoLinkDto | null;
}

let newLinks = 0;

/** A link added in the form, not yet saved. */
export function newDraftLink(value: Omit<DraftLink, 'uid' | 'id'>): DraftLink {
  newLinks += 1;
  return { ...value, uid: `new-${newLinks}`, id: null };
}

const draftLinkOf = (link: LinkDto): DraftLink => ({ ...link, uid: link.id });

/** What makes two links the same link: its type and its target work item, or for a url link its address and title. */
export const linkSignature = (link: Omit<DraftLink, 'uid' | 'id'>): string =>
  link.type === 'url'
    ? ['url', link.url ?? '', link.title ?? ''].join('|')
    : [link.type, link.workItemId ?? ''].join('|');

// The form's editable copy of a work item. Every field is a value the server sends; a save sends only what differs.
export interface Draft {
  title: string;
  description: string;
  acceptanceCriteria: ChecklistItem[];
  reproSteps: string;
  severity: number | null;
  state: StateRef;
  assignee: UserRef | null;
  sprint: SprintRef | null;
  priority: number;
  points: number | null;
  parent: ParentRefDto | null;
  tags: string[];
  /** Feature only, like statusLine: '' on every other type. */
  goal: string;
  statusLine: string;
  links: DraftLink[];
}

export type DraftField = keyof Draft;

// The field order of the strip and the main column, which is the order "Unsaved: …" lists them in.
export const draftFields: readonly DraftField[] = [
  'title',
  'state',
  'assignee',
  'sprint',
  'points',
  'priority',
  'parent',
  'tags',
  'statusLine',
  'goal',
  'description',
  'acceptanceCriteria',
  'reproSteps',
  'severity',
  'links',
];

export const fieldLabels: Record<DraftField, string> = {
  title: 'Title',
  state: 'State',
  assignee: 'Assignee',
  sprint: 'Sprint',
  points: 'Story points',
  priority: 'Priority',
  parent: 'Parent',
  tags: 'Tags',
  statusLine: 'Status',
  goal: 'Goal',
  description: 'Description',
  acceptanceCriteria: 'Acceptance criteria',
  reproSteps: 'Repro steps',
  severity: 'Severity',
  links: 'Links',
};

export function draftOf(item: WorkItemDto): Draft {
  return {
    title: item.title,
    description: item.description,
    acceptanceCriteria: item.acceptanceCriteria,
    reproSteps: item.reproSteps,
    severity: item.severity,
    state: item.state,
    assignee: item.assignee,
    sprint: item.sprint,
    priority: item.priority,
    points: item.points,
    parent: item.parent,
    tags: item.tags,
    goal: item.goal,
    statusLine: item.statusLine,
    links: item.links.map(draftLinkOf),
  };
}

// Structural equality for what a draft holds: scalars, null, refs, and lists of either.
function same(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

// A ref is the same field value when its id is: a renamed state or sprint is not an edit.
const comparable = (field: DraftField, draft: Draft): unknown => {
  switch (field) {
    case 'title':
      return draft.title.trim();
    case 'links':
      return draft.links.map((link) => link.id ?? `new|${linkSignature(link)}`);
    case 'state':
    case 'assignee':
    case 'sprint':
    case 'parent':
      return draft[field]?.id ?? null;
    default:
      return draft[field];
  }
};

export function changedFields(base: Draft, draft: Draft): DraftField[] {
  return draftFields.filter((field) => !same(comparable(field, base), comparable(field, draft)));
}

const set = <T>(value: T) => ({ value });

/** The links a draft adds (the ones without a row yet) and the saved link rows it drops. */
export function linkChangesOf(
  base: Draft,
  draft: Draft,
): { add: LinkValue[]; removeIds: string[] } {
  const kept = new Set(draft.links.map((link) => link.id));
  return {
    add: draft.links
      .filter((link) => link.id === null)
      .map(({ type, workItemId, key, url, title }) => ({ type, workItemId, key, url, title })),
    removeIds: base.links.flatMap((link) =>
      link.id !== null && !kept.has(link.id) ? [link.id] : [],
    ),
  };
}

/** The patch body's changes: a field the draft left alone is null. */
export function changesOf(base: Draft, draft: Draft): WorkItemChanges {
  const changed = new Set(changedFields(base, draft));
  const links = linkChangesOf(base, draft);
  return {
    title: changed.has('title') ? set(draft.title.trim()) : null,
    description: changed.has('description') ? set(draft.description) : null,
    acceptanceCriteria: changed.has('acceptanceCriteria') ? set(draft.acceptanceCriteria) : null,
    reproSteps: changed.has('reproSteps') ? set(draft.reproSteps) : null,
    severity: changed.has('severity') ? set(draft.severity) : null,
    state: changed.has('state') ? set(draft.state.id) : null,
    assignee: changed.has('assignee') ? set(draft.assignee?.id ?? null) : null,
    sprint: changed.has('sprint') ? set(draft.sprint?.id ?? null) : null,
    priority: changed.has('priority') ? set(draft.priority) : null,
    points: changed.has('points') ? set(draft.points) : null,
    parent: changed.has('parent') ? set(draft.parent?.id ?? null) : null,
    tags: changed.has('tags') ? set(draft.tags) : null,
    goal: changed.has('goal') ? set(draft.goal) : null,
    statusLine: changed.has('statusLine') ? set(draft.statusLine) : null,
    addLinks: links.add,
    removeLinkIds: links.removeIds,
  };
}

// The links of `mine` laid over `to`: what `mine` dropped from `from` goes (a link of `from` that had no row yet goes by
// its signature, because `to` may hold it saved), and what it added comes after, unless `to` already has it.
function rebaseLinks(from: DraftLink[], mine: DraftLink[], to: DraftLink[]): DraftLink[] {
  const kept = new Set(mine.map((link) => link.uid));
  const known = new Set(from.map((link) => link.uid));
  const dropped = from.filter((link) => !kept.has(link.uid));
  const droppedIds = new Set(dropped.flatMap((link) => (link.id === null ? [] : [link.id])));
  const droppedSignatures = new Set(dropped.filter((link) => link.id === null).map(linkSignature));
  const survivors = to.filter(
    (link) =>
      !(link.id !== null && droppedIds.has(link.id)) && !droppedSignatures.has(linkSignature(link)),
  );
  const present = new Set(survivors.map(linkSignature));
  const added = mine.filter((link) => !known.has(link.uid) && !present.has(linkSignature(link)));
  return [...survivors, ...added];
}

/**
 * `mine` laid over `to`: the fields `mine` edited since `from` keep its values, every other field takes `to`'s.
 * Used when a newer revision becomes the base while the user's edits stay, and after a save while edits were typed.
 */
export function rebase(from: Draft, mine: Draft, to: Draft): Draft {
  const edited = new Set(changedFields(from, mine));
  const result: Draft = { ...to };
  for (const field of edited) {
    if (field === 'links') result.links = rebaseLinks(from.links, mine.links, to.links);
    else Object.assign(result, { [field]: mine[field] });
  }
  return result;
}
