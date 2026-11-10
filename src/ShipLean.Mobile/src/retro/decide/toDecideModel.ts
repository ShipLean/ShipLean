// Pure rules of the To decide view (RT3a), copied from the web's By work item view with the decide filter.
import type {
  RetroCardDto,
  RetroFailureLogDto,
  RetroMergeProposalDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';

// A card with the points still undecided; the rest are left out.
export interface DecideCard extends RetroCardDto {
  points: RetroPointDto[];
}

const isFeature = (card: RetroCardDto) => card.workItem?.type.id === 'feature';

// Cards that hold an undecided point, Feature cards first, each keeping its undecided points only. The server's order
// is kept inside each group.
export function decideCards(cards: RetroCardDto[]): DecideCard[] {
  const open = cards
    .map((card) => ({ ...card, points: card.points.filter((p) => p.decision === 'undecided') }))
    .filter((card) => card.points.length > 0);
  return [...open.filter(isFeature), ...open.filter((card) => !isFeature(card))];
}

export const pointsToDecide = (cards: DecideCard[]): number =>
  cards.reduce((n, card) => n + card.points.length, 0);

const LOG_SECTIONS: { key: keyof RetroFailureLogDto; label: string }[] = [
  { key: 'blockers', label: 'Blockers' },
  { key: 'assumptionsDisproved', label: 'Assumptions disproved' },
  { key: 'friction', label: 'Friction' },
];

// The log's sections that have lines, in the web's order; none when the log is empty.
export function logSections(log: RetroFailureLogDto): { label: string; lines: string[] }[] {
  return LOG_SECTIONS.filter((s) => log[s.key].length > 0).map((s) => ({
    label: s.label,
    lines: log[s.key],
  }));
}

export const logEntryCount = (log: RetroFailureLogDto): number =>
  logSections(log).reduce((n, s) => n + s.lines.length, 0);

// "‹proposedBy› proposes merging ‹from› into ‹into›": an agent by its name, a person by theirs.
export function proposalText(p: RetroMergeProposalDto): string {
  const by = p.proposedBy.agentName ?? p.proposedBy.user.name;
  return `${by} proposes merging ${p.from.name} into ${p.into.name}`;
}

export const pointCountLabel = (n: number): string => `${n} ${n === 1 ? 'point' : 'points'}`;
