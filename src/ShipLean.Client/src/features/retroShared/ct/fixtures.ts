import sprintRetro from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/sprint-retro.json' with { type: 'json' };
import type { RetroPointDto, SprintRetroDto } from '../../../api/retros';

const view = sprintRetro as SprintRetroDto;

export const strips = view.recurring;
export const sprintNames = view.sprints.map((s) => s.name);

const allPoints = view.cards.flatMap((c) => c.points);
export const agentPoint: RetroPointDto = allPoints.find((p) => p.author.agentName !== null)!;
