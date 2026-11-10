import { useState } from 'react';
import { Text, View } from 'react-native';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import type { Pending, UndoOutcome } from '../../actions/types';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { ChangeLine } from './ChangeLine';
import { DecisionChange } from './DecisionChange';
import { decisionNumbers, UNDO_COPY, whyStayed } from './undoCopy';
import {
  undoPartialChangeId,
  undoPartialDoneId,
  undoPartialRetryId,
  undoPartialSheetId,
  undoPartialStayedId,
  undoPartialUndoneId,
} from './UndoPartialSheet.testIds';

export interface UndoPartialSheetProps {
  // The batch's actions as they were before the undo (an undone one may have left pending()).
  items: Pending[];
  outcomes: UndoOutcome[];
  // Runs the undo again; offered when some outcome failed.
  onRetry?: () => void;
  onClose: () => void;
}

// An undo that couldn't take everything back (voice D21): what was undone, what stayed and why, and Change D<n> for
// an answer that stayed, through SHIP-922's decision change.
export function UndoPartialSheet({ items, outcomes, onRetry, onClose }: UndoPartialSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const runner = useActionRunner();
  const [changing, setChanging] = useState<{ pending: Pending; number: number } | null>(null);
  const outcomeOf = new Map(outcomes.map((o) => [o.id, o.outcome]));
  const undone = items.filter((p) => outcomeOf.get(p.id) === 'undone');
  const stayed = items.filter((p) => {
    const o = outcomeOf.get(p.id);
    return o !== undefined && o !== 'undone';
  });
  const changeable = stayed.flatMap((p) =>
    outcomeOf.get(p.id) === 'irreversible' && p.action.itemId !== null
      ? decisionNumbers(p.action).map((number) => ({ pending: p, number }))
      : [],
  );
  const anyFailed = stayed.some((p) => outcomeOf.get(p.id) === 'failed');

  return (
    <BottomSheet
      title={UNDO_COPY.partialTitle(undone.length, items.length)}
      open
      onClose={onClose}
      testID={undoPartialSheetId}
    >
      {changing !== null && changing.pending.action.itemId !== null ? (
        <DecisionChange
          itemId={changing.pending.action.itemId}
          itemKey={changing.pending.action.key ?? ''}
          number={changing.number}
          onDone={onClose}
        />
      ) : (
        <View style={{ gap: theme.space[3] }}>
          {undone.length === 0 ? null : (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.xs }}>
              {UNDO_COPY.undoneHeading}
            </Text>
          )}
          {undone.map((p) => (
            <ChangeLine
              key={p.id}
              testID={undoPartialUndoneId(p.id)}
              text={runner.describe(p.action).text}
            />
          ))}
          <Text style={{ color: colors.muted, fontSize: theme.font.size.xs }}>
            {UNDO_COPY.stayedHeading}
          </Text>
          {stayed.map((p) => (
            <ChangeLine
              key={p.id}
              testID={undoPartialStayedId(p.id)}
              text={runner.describe(p.action).text}
              detail={whyStayed(p.action.kind, outcomeOf.get(p.id) as 'irreversible' | 'failed')}
            />
          ))}
          {changeable.length === 0 ? null : (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
              {UNDO_COPY.changeHint}
            </Text>
          )}
          {changeable.map((c) => (
            <Button
              key={`${c.pending.id}-${c.number}`}
              testID={undoPartialChangeId(c.number)}
              variant="ghost"
              label={`Change D${c.number}`}
              onPress={() => setChanging(c)}
            />
          ))}
          {anyFailed && onRetry !== undefined ? (
            <Button testID={undoPartialRetryId} label="Try again" onPress={onRetry} />
          ) : null}
          <Button testID={undoPartialDoneId} variant="ghost" label="Done" onPress={onClose} />
        </View>
      )}
    </BottomSheet>
  );
}
