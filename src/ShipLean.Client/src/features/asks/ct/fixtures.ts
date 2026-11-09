import historyJson from '../../../../../ShipLean.TestSupport/DtoFixtures/asks/history-asks.json' with { type: 'json' };
import featureJson from '../../../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.feature.json' with { type: 'json' };
import storyJson from '../../../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.story.json' with { type: 'json' };
import type { DecisionDto, ItemAsksDto, QuestionDto } from '../../../api/asks';
import type { HistoryDto } from '../../../api/workItems';

// ItemAsksDto values named after the design's states (decisions.html?state=…), all built from the C# fixture JSON so a
// fixture correction flows through. api/asks.test.ts pins the JSON against the types.

const story = storyJson as unknown as ItemAsksDto;
const featureAsks = featureJson as unknown as ItemAsksDto;
const fresh = (asks: ItemAsksDto): ItemAsksDto => structuredClone(asks);
const isOpen = (ask: QuestionDto | DecisionDto) => ask.state === 'open';
const counted = (asks: ItemAsksDto): ItemAsksDto => ({
  ...asks,
  openQuestions: asks.questions.filter(isOpen).length,
  openDecisions: asks.decisions.filter(isOpen).length,
});

/** No asks at all: what stubApi answers by default. */
export const emptyAsks = (workItemId: string): ItemAsksDto => ({
  workItemId,
  owner: null,
  ownerIsCreator: false,
  ownerIsMe: false,
  openQuestions: 0,
  openDecisions: 0,
  questions: [],
  decisions: [],
  waitingChildren: [],
});

/** A Story waiting on me: open questions and decisions next to settled ones. */
export const waiting = fresh(story);

/** Waiting on someone else; I can answer too. */
export const other: ItemAsksDto = { ...fresh(story), owner: featureAsks.owner, ownerIsMe: false };

/** Nobody assigned, so it waits on the creator. */
export const unassigned: ItemAsksDto = {
  ...fresh(story),
  owner: {
    id: '0199f000-0000-7000-8000-000000000002',
    name: 'Ana Costa',
    initials: 'AC',
    avatarColour: '#2f7fd8',
    photoUrl: null,
  },
  ownerIsMe: false,
  ownerIsCreator: true,
};

/** The first open question answered and every open decision confirmed with its recommended answer. */
export const answered: ItemAsksDto = (() => {
  const asks = fresh(story);
  const answer = asks.questions.find((question) => question.answer)?.answer ?? null;
  const confirmedEvent = asks.decisions
    .flatMap((decision) => decision.history)
    .find((event) => event.kind === 'confirmed');
  if (!answer || !confirmedEvent)
    throw new Error('item-asks.story.json lost its answer or its confirmed event');
  const firstOpen = asks.questions.find(isOpen);
  if (firstOpen) Object.assign(firstOpen, { state: 'answered', answer });
  for (const decision of asks.decisions.filter(isOpen)) {
    Object.assign(decision, {
      state: 'confirmed',
      answeredBy: confirmedEvent.by,
      answeredAgentName: confirmedEvent.agentName,
      answeredAt: confirmedEvent.at,
      answeredAtLabel: confirmedEvent.atLabel,
    });
    decision.history.push({ ...confirmedEvent, to: decision.recommended, toKind: 'recommended' });
  }
  return counted(asks);
})();

/** The same asks as answered; the fold is UI state. */
export const folded: ItemAsksDto = fresh(answered);

/** Only the settled asks of the Story: nothing open, nothing waiting. */
export const settled: ItemAsksDto = (() => {
  const asks = fresh(story);
  return counted({
    ...asks,
    questions: asks.questions.filter((question) => !isOpen(question)),
    decisions: asks.decisions.filter((decision) => !isOpen(decision)),
  });
})();

/** The Story with decision history to open (the design opens D3's). */
export const history = fresh(story);

/** No questions, decisions or waiting children. */
export const empty: ItemAsksDto = emptyAsks(story.workItemId);

/** A Feature with its own settled asks and Stories below it that wait. */
export const feature = fresh(featureAsks);

/** A Feature whose only waiting is on its children. */
export const featureChildrenOnly: ItemAsksDto = counted({
  ...fresh(featureAsks),
  questions: [],
  decisions: [],
});

/** A work item's History with ask lines, for the rail. */
export const askHistory = historyJson as unknown as HistoryDto;
