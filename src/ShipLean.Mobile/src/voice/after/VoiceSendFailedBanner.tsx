import { useState } from 'react';
import { Text, View } from 'react-native';
import { useActionRunner, usePending } from '../../actions/ActionRunnerProvider';
import type { Pending } from '../../actions/types';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { ChangeLine } from './ChangeLine';
import { UNDO_COPY } from './undoCopy';
import {
  voiceSendFailedAppliedId,
  voiceSendFailedDismissId,
  voiceSendFailedId,
  voiceSendFailedRefusedId,
} from './VoiceSendFailedBanner.testIds';

export interface VoiceSendFailedBannerProps {
  // The item whose refused voice changes to show; without one, every refused voice change.
  itemId?: string;
  itemKey?: string;
}

const isRefused = (p: Pending) =>
  p.origin === 'voice' && p.state === 'failed' && p.failure !== null && p.undoOutcome === null;

// Queued voice changes ShipLean refused when they were sent (voice D18): which one and the runner's reason, and what
// of the same batch went through. There is no push: this banner is the only place it shows.
export function VoiceSendFailedBanner({ itemId, itemKey }: VoiceSendFailedBannerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const runner = useActionRunner();
  const pending = usePending((p) => p.origin === 'voice');
  // Dismissing hides the banner here; undoing the batch is the row's Undo, not this.
  const [dismissed, setDismissed] = useState<ReadonlySet<string>>(new Set());
  const refused = pending.filter(
    (p) =>
      isRefused(p) && !dismissed.has(p.id) && (itemId === undefined || p.action.itemId === itemId),
  );
  const batchIds = [...new Set(refused.map((p) => p.batchId))];
  const text = (p: Pending) => runner.describe(p.action).text;
  const title =
    itemId === undefined || batchIds.length !== 1
      ? UNDO_COPY.refusedAllTitle(refused.length)
      : UNDO_COPY.refusedTitle(
          refused.length,
          pending.filter((p) => p.batchId === batchIds[0]).length,
        );

  return (
    <View testID={voiceSendFailedId}>
      {refused.length === 0 ? null : (
        <View
          style={{
            gap: theme.space[2],
            padding: theme.space[3],
            borderRadius: theme.radius.md,
            borderWidth: 1,
            borderColor: colors.danger,
          }}
        >
          <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
            {itemKey === undefined ? title : `${title} · ${itemKey}`}
          </Text>
          {refused.map((p) => (
            <ChangeLine
              key={p.id}
              testID={voiceSendFailedRefusedId(p.id)}
              text={itemId === undefined ? `${p.action.key ?? ''} · “${text(p)}”` : `“${text(p)}”`}
              detail={p.error ?? undefined}
            />
          ))}
          {batchIds.map((batchId) => {
            const went = pending.filter(
              (p) => p.batchId === batchId && p.state === 'sent' && p.undoOutcome === null,
            );
            return went.length === 0 ? null : (
              <Text
                key={batchId}
                testID={voiceSendFailedAppliedId(batchId)}
                style={{ color: colors.muted, fontSize: theme.font.size.sm }}
              >
                {UNDO_COPY.wentThrough(went.map(text))}
              </Text>
            );
          })}
          <Button
            testID={voiceSendFailedDismissId}
            variant="ghost"
            label="Dismiss"
            onPress={() => setDismissed(new Set([...dismissed, ...refused.map((p) => p.id)]))}
          />
        </View>
      )}
    </View>
  );
}
