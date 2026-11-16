import { describe, expect, it } from 'vitest';
import mcpFixture from '../../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-mcp.json' with { type: 'json' };
import runningFixture from '../../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-running-view.json' with { type: 'json' };
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE } from './ct/stubFeatureTimeline';
import { toMcpJson } from './toMcpJson';

describe('toMcpJson', () => {
  it('ShouldMatchTheMcpFixture', () => {
    const expected = structuredClone(mcpFixture) as Record<string, unknown>;
    delete expected.project;
    delete expected.open_url;
    expect(toMcpJson(FINISHED_TIMELINE)).toEqual(expected);
  });

  it('ShouldSnakeCaseEnumValuesButNotFreeText', () => {
    const json = JSON.stringify(toMcpJson(FINISHED_TIMELINE));
    expect(json).toContain('"point":"execution_started"');
    expect(json).toContain('"verdict":"changes_requested"');
    expect(json).not.toContain('executionStarted');
    expect(json).toContain('"source":"Feature created"');
  });

  it('ShouldSnakeCaseInFlightBarStates', () => {
    const running = runningFixture as unknown as FeatureTimelineViewDto;
    const states = toMcpJson(running).bars as { state: string }[];
    expect(states.map((b) => b.state)).not.toContain('inFlight');
  });
});
