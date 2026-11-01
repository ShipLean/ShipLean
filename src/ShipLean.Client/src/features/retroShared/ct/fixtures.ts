import sprintRetro from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/sprint-retro.json' with { type: 'json' };
import type { RetroCardDto, RetroPointDto, SprintRetroDto } from '../../../api/retros';

const view = sprintRetro as SprintRetroDto;

/** The pinned sprint-retro fixture's cards: the conductor's feature retro, a sub-agent's story retro and a person's sprint-level one. */
export const featureCard: RetroCardDto = view.cards.find((c) => c.workItem?.type.id === 'feature')!;
export const subAgentCard: RetroCardDto = view.cards.find(
  (c) => c.author.role === 'subAgent' && c.failureLog.friction.length > 0,
)!;
export const sprintCard: RetroCardDto = view.cards.find((c) => c.workItem === null)!;
export const strips = view.recurring;
export const sprintNames = view.sprints.map((s) => s.name);

const allPoints = view.cards.flatMap((c) => c.points);
export const badPoint: RetroPointDto = allPoints.find(
  (p) => p.category === 'wentBadly' && p.rootCause,
)!;
export const goodPoint: RetroPointDto = allPoints.find((p) => p.category === 'wentWell')!;
export const agentPoint: RetroPointDto = allPoints.find((p) => p.author.agentName !== null)!;
export const actionPoint: RetroPointDto = allPoints.find((p) => p.action !== null)!;
