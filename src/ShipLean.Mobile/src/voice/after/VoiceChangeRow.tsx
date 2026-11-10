import { getHistory, historyKeys } from '@shiplean/core/api/history';
import { getMe, meKey } from '@shiplean/core/api/me';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { View } from 'react-native';
import { useActionRunner, useRunnerChanges } from '../../actions/ActionRunnerProvider';
import { systemNow } from '../../actions/clock';
import { useTheme } from '../../theme';
import { UndoPreviewSheet } from './UndoPreviewSheet';
import { VoiceChangeEntry } from './VoiceChangeEntry';
import { voiceChangeRowId } from './VoiceChangeRow.testIds';
import { historyChanges, voiceChanges, type VoiceChange } from './voiceChanges';

export interface VoiceChangeRowProps {
  itemId: string;
  itemKey: string;
}

// "Your voice change" in an item's Notes (voice D16, D19, D20): a queued batch shows Waiting to send and Undo drops
// it; a sent one keeps Undo, previewed first, for 10 minutes or until someone else changes the item.
export function VoiceChangeRow({ itemId, itemKey }: VoiceChangeRowProps) {
  const theme = useTheme();
  const runner = useActionRunner();
  useRunnerChanges(runner);
  const [, setTick] = useState(0);
  const [previewing, setPreviewing] = useState<VoiceChange | null>(null);
  const pending = runner.pending();
  // Only a sent batch needs to know who changed the item since; a waiting one makes no request.
  const anySent = pending.some(
    (p) =>
      p.origin === 'voice' &&
      p.action.itemId === itemId &&
      (p.state === 'sent' || p.state === 'failed'),
  );
  const history = useQuery({
    queryKey: historyKeys.item(itemId),
    queryFn: () => getHistory(itemId),
    enabled: anySent,
  });
  const me = useQuery({ queryKey: meKey, queryFn: () => getMe(), enabled: anySent });
  const now = systemNow();
  const known = history.data !== undefined && me.data !== undefined;
  const changes = voiceChanges(
    pending,
    itemId,
    known ? historyChanges(history.data) : [],
    me.data?.id ?? '',
    now,
  ).filter(
    // A sent batch shows once it's known whether someone else changed the item, and goes when its window ends.
    (c) => c.state === 'waiting' || (known && (c.undoable || c.changedSince)),
  );

  const nextEnd = changes
    .filter((c) => c.undoable && c.undoUntil !== null)
    .map((c) => Date.parse(c.undoUntil as string))
    .reduce<number | null>((a, b) => (a === null || b < a ? b : a), null);
  const at = now.getTime();
  // Render again when the soonest window ends, so its Undo goes.
  useEffect(() => {
    if (nextEnd === null) return;
    const timer = setTimeout(() => setTick((t) => t + 1), Math.max(0, nextEnd - at));
    return () => clearTimeout(timer);
  }, [nextEnd, at]);

  return (
    <View testID={voiceChangeRowId} style={{ gap: theme.space[2] }}>
      {changes.map((c) => (
        <VoiceChangeEntry
          key={c.batchId}
          change={c}
          onUndo={() => {
            // Nothing has gone out: dropping it from the queue undoes it, with no request and nothing to preview.
            if (c.state === 'waiting') void runner.undo(c.batchId);
            else setPreviewing(c);
          }}
        />
      ))}
      {previewing === null ? null : (
        <UndoPreviewSheet
          batchId={previewing.batchId}
          itemKey={itemKey}
          items={previewing.items}
          onClose={() => setPreviewing(null)}
        />
      )}
    </View>
  );
}
