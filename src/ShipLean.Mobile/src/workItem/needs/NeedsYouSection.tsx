import type { DecisionDto, QuestionDto, WaitingChildDto } from '@shiplean/core/api/asks';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import { useEffect, useRef, type FC } from 'react';
import { Pressable, Text, View } from 'react-native';
import type { PendingAnswer } from '../../inbox/actions';
import { useInboxActions } from '../../inbox/InboxActions';
import { useInboxSheets } from '../../inbox/InboxSheets';
import { toInboxItem } from '../../inbox/toInboxItem';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { useVoice } from '../../voice/VoiceProvider';
import { useInvalidateWorkItem, useItemAsksQuery } from '../queries';
import {
  needsAgentTagId,
  needsAskTextId,
  needsBtnId,
  needsCardId,
  needsChildId,
  needsFromId,
  needsKindId,
  needsPendingId,
  needsRetryAnswerId,
  needsYouRetryId,
  needsSayId,
  needsUndoId,
  needsYouErrorId,
  needsYouSectionId,
  needsYouTitleId,
} from '../testIds';

export interface NeedsYouSectionProps {
  item: WorkItemDto;
  projectKey: string;
}

const byNumber = <T extends { number: number }>(a: T, b: T) => a.number - b.number;

// "1 question", "2 decisions". Null for none.
const count = (n: number, one: string, many: string) =>
  n === 0 ? null : `${n} ${n === 1 ? one : many}`;

const childLine = (child: WaitingChildDto) =>
  [
    child.key,
    count(child.openQuestions, 'question', 'questions'),
    count(child.openDecisions, 'decision', 'decisions'),
  ]
    .filter((part) => part !== null)
    .join(' · ');

// The runner's latest answer to this ask that is still in play (an undone one is gone).
function answerTo(label: string, itemId: string, pending: PendingAnswer[]): PendingAnswer | null {
  const mine = pending.filter(
    (p) => p.action.itemId === itemId && p.undoOutcome !== 'undone' && p.labels.includes(label),
  );
  return mine[mine.length - 1] ?? null;
}

// What an answer in the runner says in place of the buttons.
function stateLine(p: PendingAnswer, key: string): string {
  switch (p.state) {
    case 'undo':
      return p.action.kind === 'approve' ? 'Approving…' : 'Sending…';
    case 'queued':
      return 'Waiting to send';
    case 'sending':
    case 'uncertain':
      return 'Sending…';
    case 'failed':
      return "Didn't send";
    case 'sent':
      return `Posted on ${key}. The next agent to pick it up reads it.`;
  }
}

// The asks read needs the project id, which a cold open or deep link resolves after the first render: nothing is read
// until it is known.
export const NeedsYouSection: FC<NeedsYouSectionProps> = ({ item, projectKey }) => {
  const { project } = useProject(projectKey);
  if (project === undefined) return <View testID={needsYouSectionId} />;
  return <Asks item={item} projectKey={projectKey} projectId={project.id} />;
};

function Asks({ item, projectKey, projectId }: NeedsYouSectionProps & { projectId: string }) {
  const theme = useTheme();
  const { colors } = theme;
  const router = useRouter();
  const query = useItemAsksQuery(projectId, item.key, item.id);
  const { dispatch, undo, retry, pending } = useInboxActions();
  const sheets = useInboxSheets();
  const voice = useVoice();
  const invalidate = useInvalidateWorkItem();

  // When an answer to this item has been sent, the asks read is stale: read it again once.
  const mine = pending.filter((p) => p.action.itemId === item.id);
  const sentBefore = useRef(new Set<string>());
  useEffect(() => {
    let fresh = false;
    for (const p of mine) {
      if (p.state === 'sent' && !sentBefore.current.has(p.id)) {
        sentBefore.current.add(p.id);
        fresh = true;
      }
    }
    if (fresh) void invalidate(projectId, item.key, item.id);
  });

  const root = { testID: needsYouSectionId };
  if (query.isError) {
    return (
      <View {...root} style={{ padding: theme.space[4], gap: theme.space[2] }}>
        <Text
          testID={needsYouErrorId}
          accessibilityRole="alert"
          style={{ color: colors.danger, fontSize: theme.font.size.sm }}
        >
          {`Couldn't load what is waiting on you. ${query.error.message}`}
        </Text>
        <View style={{ flexDirection: 'row' }}>
          <Button
            testID={needsYouRetryId}
            label="Retry"
            variant="ghost"
            busy={query.isFetching}
            onPress={() => void query.refetch()}
          />
        </View>
      </View>
    );
  }
  const asks = query.data;
  const decisions = (asks?.decisions ?? []).filter((d) => d.state === 'open').sort(byNumber);
  const questions = (asks?.questions ?? []).filter((q) => q.state === 'open').sort(byNumber);
  const children = asks?.waitingChildren ?? [];
  if (asks === undefined || decisions.length + questions.length + children.length === 0) {
    return <View {...root} />;
  }

  const inboxItem = toInboxItem(item, asks);
  const showSay = voice.state.kind !== 'disabled';
  const small = { fontSize: theme.font.size.sm, color: colors.muted };

  const pick = (label: string) => inboxItem.asks.find((a) => a.label === label)!;
  const target = { area: 'inbox', projectId, itemId: item.id, key: item.key } as const;

  const card = (
    label: string,
    heading: string,
    ask: { who: string | null; agent: string | null },
    say: string,
    buttons: {
      id: 'approve' | 'discuss' | 'reply';
      label: string;
      primary: boolean;
      press: () => void;
    }[],
  ) => {
    const answer = answerTo(label, item.id, pending);
    return (
      <View
        key={label}
        testID={needsCardId(label)}
        style={{
          marginHorizontal: theme.space[4],
          marginBottom: theme.space[3],
          padding: theme.space[3] + 2,
          borderRadius: 14,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surface,
        }}
      >
        <Text
          testID={needsKindId(label)}
          style={{
            color: colors.warn,
            fontSize: theme.font.size.xs,
            fontWeight: theme.font.weight.bold,
            letterSpacing: 0.5,
            textTransform: 'uppercase',
          }}
        >
          {label.startsWith('D') ? 'Decision to confirm' : 'Needs input'}
        </Text>
        <Text
          testID={needsAskTextId(label)}
          style={{
            marginTop: 6,
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.medium,
            lineHeight: 20,
          }}
        >
          {heading}
        </Text>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 4 }}>
          <Text testID={needsFromId(label)} style={small}>
            {ask.agent ?? ask.who ?? ''}
          </Text>
          {ask.agent === null ? null : (
            <Text
              testID={needsAgentTagId(label)}
              style={{
                ...small,
                fontSize: theme.font.size.xs,
                fontWeight: theme.font.weight.semibold,
                textTransform: 'uppercase',
              }}
            >
              Agent
            </Text>
          )}
        </View>
        {answer === null ? (
          <>
            <View
              style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2], marginTop: 12 }}
            >
              {buttons.map((b) => (
                <Button
                  key={b.id}
                  testID={needsBtnId(label, b.id)}
                  label={b.label}
                  variant={b.primary ? 'primary' : 'ghost'}
                  onPress={b.press}
                />
              ))}
            </View>
            {showSay ? (
              <Text testID={needsSayId(label)} style={[small, { marginTop: 10 }]}>
                {`Or say “${say}”`}
              </Text>
            ) : null}
          </>
        ) : (
          <View style={{ marginTop: 12, gap: theme.space[2] }}>
            <Text testID={needsPendingId(label)} style={small}>
              {stateLine(answer, item.key)}
              {answer.state === 'failed' && answer.error !== null ? ` ${answer.error}` : ''}
            </Text>
            {answer.state === 'undo' ? (
              <View style={{ flexDirection: 'row' }}>
                <Button
                  testID={needsUndoId(label)}
                  label="Undo"
                  variant="ghost"
                  onPress={() => undo(answer.batchId)}
                />
              </View>
            ) : null}
            {answer.state === 'failed' ? (
              <View style={{ flexDirection: 'row' }}>
                <Button
                  testID={needsRetryAnswerId(label)}
                  label="Retry"
                  variant="ghost"
                  onPress={() => retry(answer.id)}
                />
              </View>
            ) : null}
          </View>
        )}
      </View>
    );
  };

  const decisionCard = (d: DecisionDto) =>
    card(
      d.label,
      `${d.label} · ${d.question}`,
      { who: d.raisedBy.name, agent: d.raisedAgentName },
      `approve ${d.label}`,
      [
        {
          id: 'approve',
          label: 'Approve',
          primary: true,
          press: () => dispatch({ ...target, kind: 'approve', numbers: [d.number] }),
        },
        {
          id: 'discuss',
          label: 'Discuss',
          primary: false,
          press: () =>
            sheets.openAnswer({ mode: 'discuss', item: inboxItem, ask: pick(d.label), projectId }),
        },
      ],
    );

  const questionCard = (q: QuestionDto) =>
    card(
      q.label,
      `${q.label} · ${q.text}`,
      { who: q.askedBy.name, agent: q.agentName },
      'reply: …',
      [
        {
          id: 'reply',
          label: 'Reply',
          primary: false,
          press: () =>
            sheets.openAnswer({ mode: 'question', item: inboxItem, ask: pick(q.label), projectId }),
        },
      ],
    );

  return (
    <View {...root}>
      <Text
        testID={needsYouTitleId}
        accessibilityRole="header"
        style={{
          paddingHorizontal: theme.space[4],
          paddingTop: theme.space[3],
          paddingBottom: theme.space[2],
          color: colors.muted,
          fontSize: theme.font.size.xs,
          fontWeight: theme.font.weight.bold,
          letterSpacing: 0.5,
          textTransform: 'uppercase',
        }}
      >
        Needs you
      </Text>
      {decisions.map(decisionCard)}
      {questions.map(questionCard)}
      {children.map((child) => (
        <Pressable
          key={child.key}
          testID={needsChildId(child.key)}
          accessibilityRole="button"
          onPress={() => router.push(links.item(projectKey, child.key))}
          style={{
            minHeight: 44,
            justifyContent: 'center',
            paddingHorizontal: theme.space[4],
            borderTopWidth: 1,
            borderTopColor: colors.border,
          }}
        >
          <Text style={{ color: colors.accent, fontSize: theme.font.size.body15 }}>
            {childLine(child)}
          </Text>
        </Pressable>
      ))}
      <Text style={[small, { paddingHorizontal: theme.space[4], paddingTop: theme.space[2] }]}>
        {`Answers are posted on ${item.key}. The next agent to pick it up reads them.`}
      </Text>
    </View>
  );
}
