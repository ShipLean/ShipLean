import type { ItemAsksDto } from '@shiplean/core/api/asks';
import { describe, expect, it } from 'vitest';
import storyAsks from '../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.story.json';
import { aStory, aWorkItem } from '../test-support/builders';
import { statusAsk, toInboxItem } from './toInboxItem';

describe('toInboxItem', () => {
  it('makes open decisions and questions asks labelled Dn and Qn, and a Feature status line a Status ask', () => {
    const asks = storyAsks as ItemAsksDto;
    const story = aStory();
    const item = toInboxItem(story, asks);
    expect(item).toMatchObject({
      id: story.id,
      key: story.key,
      type: 'story',
      title: story.title,
      revision: story.revision,
    });
    // Q2 is answered and D5–D7 are decided, so they are left out.
    expect(item.asks.map((a) => [a.kind, a.label])).toEqual([
      ['question', 'Q1'],
      ['decision', 'D1'],
      ['decision', 'D2'],
      ['decision', 'D3'],
      ['decision', 'D4'],
    ]);
    const d1 = asks.decisions[0];
    expect(item.asks[1].decision).toEqual({
      number: 1,
      question: d1.question,
      recommended: d1.recommended,
      alternatives: d1.alternatives,
    });
    expect(item.asks[0].question?.number).toBe(1);
    expect(item.oldestAt).toBe(d1.raisedAt);
    expect(statusAsk(story)).toBeNull();

    const feature = aWorkItem();
    const verified = feature.statusVerified;
    const status = toInboxItem(feature, null).asks;
    expect(status).toHaveLength(1);
    expect(status[0]).toMatchObject({
      kind: 'status',
      label: 'Status',
      at: verified?.at,
      from: verified?.by,
      agentName: verified?.agent,
      status: { statusLine: feature.statusLine, verifiedAt: verified?.at },
    });
    expect(statusAsk(aWorkItem({ statusLine: '' }))).toBeNull();
    expect(statusAsk(aWorkItem({ statusVerified: null }))?.at).toBe(feature.createdAt);
  });
});
