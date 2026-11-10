import {
  asksKeys,
  changeDecision,
  getItemAsks,
  type ChangeDecisionRequest,
} from '@shiplean/core/api/asks';
import { commentKeys } from '@shiplean/core/api/commentsApi';
import { historyKeys } from '@shiplean/core/api/history';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { Chip } from '../../ui/Chip';
import { TextField } from '../../ui/TextField';
import { UNDO_COPY } from './undoCopy';
import {
  decisionChangeErrorId,
  decisionChangeId,
  decisionChangeOptionId,
  decisionChangeOwnTextId,
  decisionChangePostedId,
  decisionChangeSubmitId,
} from './UndoPartialSheet.testIds';

export interface DecisionChangeProps {
  itemId: string;
  itemKey: string;
  number: number;
  onDone: () => void;
}

// 'recommended', an alternative's index, or 'own'.
type Choice = 'recommended' | number | 'own';

function requestOf(choice: Choice, text: string): ChangeDecisionRequest {
  if (choice === 'own') return { kind: 'own', alternativeIndex: null, text, note: null };
  if (choice === 'recommended')
    return { kind: 'recommended', alternativeIndex: null, text: null, note: null };
  return { kind: 'alternative', alternativeIndex: choice, text: null, note: null };
}

// Change D<n> after an undo that couldn't take its answer back (voice D21, SHIP-922): another answer or your own.
export function DecisionChange({ itemId, itemKey, number, onDone }: DecisionChangeProps) {
  const theme = useTheme();
  const { colors } = theme;
  const queryClient = useQueryClient();
  // TODO: read under asksKeys.item(projectId, itemKey) once the conductor's contract fix "projectId on backlog approve
  // actions" lands; until then this private key doesn't share a cache with the Work item's asks panel.
  const asksKey = ['voice-after', 'asks', itemId];
  const asks = useQuery({ queryKey: asksKey, queryFn: () => getItemAsks(itemId) });
  const [choice, setChoice] = useState<Choice | null>(null);
  const [text, setText] = useState('');
  const [sent, setSent] = useState<'no' | 'sending' | 'posted' | 'failed'>('no');
  // The one write here that isn't an AppAction: the decision change replaces an undo the runner couldn't do (D21).
  const post = async (request: ChangeDecisionRequest) => {
    setSent('sending');
    try {
      await changeDecision(itemId, number, request);
    } catch {
      setSent('failed');
      return;
    }
    setSent('posted');
    // The item's history, comments and asks are out of date now, and so is what's waiting on me; anything read
    // under the item's key (the item and its asks) too.
    void Promise.all([
      queryClient.invalidateQueries({ queryKey: historyKeys.item(itemId) }),
      queryClient.invalidateQueries({ queryKey: commentKeys.list(itemId) }),
      queryClient.invalidateQueries({ queryKey: asksKey }),
      queryClient.invalidateQueries({ queryKey: asksKeys.waiting }),
      queryClient.invalidateQueries({ predicate: (q) => q.queryKey.includes(itemKey) }),
    ]);
  };
  const decision = asks.data?.decisions.find((d) => d.number === number);
  const label = `D${number}`;

  if (sent === 'posted')
    return (
      <View testID={decisionChangeId} style={{ gap: theme.space[3] }}>
        <Text
          testID={decisionChangePostedId}
          style={{ color: colors.text, fontSize: theme.font.size.body15 }}
        >
          {UNDO_COPY.posted(itemKey)}
        </Text>
        <Button testID={decisionChangeSubmitId} label="Done" onPress={onDone} />
      </View>
    );

  const ready = choice !== null && (choice !== 'own' || text.trim().length > 0);
  return (
    <View testID={decisionChangeId} style={{ gap: theme.space[3] }}>
      <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
        {decision === undefined ? label : `${label}: ${decision.question}`}
      </Text>
      {decision === undefined ? null : (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
          {decision.answerKind === 'recommended' ? null : (
            <Chip
              testID={decisionChangeOptionId('recommended')}
              label={decision.recommended}
              selected={choice === 'recommended'}
              onPress={() => setChoice('recommended')}
            />
          )}
          {decision.alternatives.map((alternative, i) =>
            decision.answerKind === 'alternative' && decision.alternativeIndex === i ? null : (
              <Chip
                key={alternative}
                testID={decisionChangeOptionId(String(i))}
                label={alternative}
                selected={choice === i}
                onPress={() => setChoice(i)}
              />
            ),
          )}
          <Chip
            testID={decisionChangeOptionId('own')}
            label="Your own answer"
            selected={choice === 'own'}
            onPress={() => setChoice('own')}
          />
        </View>
      )}
      {choice === 'own' ? (
        <TextField
          testID={decisionChangeOwnTextId}
          label="Your answer"
          value={text}
          onChangeText={setText}
        />
      ) : null}
      {sent === 'failed' ? (
        <Text testID={decisionChangeErrorId} style={{ color: colors.danger }}>
          {UNDO_COPY.changeFailed}
        </Text>
      ) : null}
      <Button
        testID={decisionChangeSubmitId}
        label={`Change ${label}`}
        disabled={!ready}
        busy={sent === 'sending'}
        onPress={() => {
          if (choice !== null) void post(requestOf(choice, text.trim()));
        }}
      />
    </View>
  );
}
