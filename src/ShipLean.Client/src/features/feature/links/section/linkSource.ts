import type { LinkCardDto } from '../../../../api/linkCards';

const providerOf = (kind: LinkCardDto['kind']) => (kind.startsWith('gitlab') ? 'GitLab' : 'GitHub');

/** The badge text for the card's kind. */
export const kindBadge = (kind: LinkCardDto['kind']): string => {
  switch (kind) {
    case 'githubPull':
    case 'githubIssue':
      return 'GH';
    case 'gitlabMergeRequest':
    case 'gitlabIssue':
      return 'GL';
    case 'page':
      return 'W';
  }
};

/** Where the state came from, in the server's words: `live · 4 min ago`, `reported by <agent> · 3 h ago`. Null when unknown. */
export function sourceLabel(card: LinkCardDto): string | null {
  const age = card.stateAsOfLabel ? ` · ${card.stateAsOfLabel}` : '';
  switch (card.stateSource) {
    case 'live':
      return `live${age}`;
    case 'webhook':
      return `${providerOf(card.kind)} webhook${age}`;
    case 'reported':
      return `reported by ${card.reportedBy ?? 'an agent'}${age}`;
    case 'none':
      return null;
  }
}

/** The line under the title: where it lives, or just the host (with "no preview" when there is none to show). */
export function locationLabel(card: LinkCardDto): string {
  if (card.location) return card.location;
  return card.previewStatus === 'ok' ? card.host : `${card.host} · no preview`;
}
