// A work item's links as cards. C#: ShipLean.Contracts.Links. Nothing is omitted: absent values are null; timestamps are ISO
// strings and labels are formatted by the server.
import type { LinkType } from './persisted';
import type { StateRef, TypeRef } from './refs';

export const LINK_KINDS = [
  'githubPull',
  'githubIssue',
  'gitlabMergeRequest',
  'gitlabIssue',
  'page',
] as const;
export type LinkKind = (typeof LINK_KINDS)[number];

export const LINK_GROUPS = ['code', 'page'] as const;
export type LinkGroup = (typeof LINK_GROUPS)[number];

/** Where the title came from: the provider's API, the page's own title, a person, or just the address. */
export const LINK_TITLE_SOURCES = ['live', 'fetched', 'user', 'url'] as const;
export type LinkTitleSource = (typeof LINK_TITLE_SOURCES)[number];

export const LINK_STATES = ['open', 'draft', 'merged', 'closed'] as const;
export type LinkState = (typeof LINK_STATES)[number];

/** How the state is known: read from the provider, reported by an agent, reported by a provider's webhook, or not known. */
export const LINK_STATE_SOURCES = ['live', 'reported', 'webhook', 'none'] as const;
export type LinkStateSource = (typeof LINK_STATE_SOURCES)[number];

/** `private` is an address ShipLean refuses to call. */
export const LINK_PREVIEW_STATUSES = ['ok', 'unreachable', 'private', 'none'] as const;
export type LinkPreviewStatus = (typeof LINK_PREVIEW_STATUSES)[number];

export interface WorkItemLinkRowDto {
  id: string;
  type: LinkType;
  workItemId: string;
  key: string;
  title: string;
  itemType: TypeRef;
  state: StateRef;
}

export interface LinkCardDto {
  id: string;
  url: string;
  kind: LinkKind;
  group: LinkGroup;
  title: string;
  titleSource: LinkTitleSource;
  note: string | null;
  number: number | null;
  /** "acme/storefront", or "gitlab.example.com · payments/vault". */
  location: string | null;
  host: string;
  author: string | null;
  state: LinkState | null;
  stateSource: LinkStateSource;
  stateAsOf: string | null;
  stateAsOfLabel: string | null;
  reportedBy: string | null;
  description: string | null;
  /** "/api/link-cards/{id}/image", never an external address. */
  imageUrl: string | null;
  /** "/api/link-cards/{id}/favicon", never an external address. */
  faviconUrl: string | null;
  previewStatus: LinkPreviewStatus;
  refreshDue: boolean;
}

export interface LinkCardsDto {
  workItems: WorkItemLinkRowDto[];
  code: LinkCardDto[];
  pages: LinkCardDto[];
}

/** What adding a URL would show, before anything is saved. */
export interface LinkPreviewDto {
  url: string;
  kind: LinkKind;
  group: LinkGroup;
  title: string;
  titleSource: LinkTitleSource;
  number: number | null;
  location: string | null;
  host: string;
  author: string | null;
  state: LinkState | null;
  stateSource: LinkStateSource;
  description: string | null;
  imageUrl: string | null;
  faviconUrl: string | null;
  previewStatus: LinkPreviewStatus;
  /** One line for the add-link form, e.g. "GitHub pull request · state stays live". */
  message: string;
}

/** POST api/work-items/{id}/link-cards/preview */
export interface PreviewLinkRequest {
  url: string;
}

/** POST api/work-items/{id}/link-cards (201 LinkCardDto) */
export interface AddLinkCardRequest {
  url: string;
  title?: string | null;
  note?: string | null;
}

/** POST api/work-items/{id}/work-item-links (201 WorkItemLinkRowDto). `targetKey` is the other item's key, e.g. "SHIP-142". */
export interface AddWorkItemLinkRequest {
  targetKey: string;
  type: Exclude<LinkType, 'url'>;
}

/** PATCH api/link-cards/{id}. A field left out is unchanged; an empty string clears it. */
export interface EditLinkCardRequest {
  title?: string;
  note?: string;
}
