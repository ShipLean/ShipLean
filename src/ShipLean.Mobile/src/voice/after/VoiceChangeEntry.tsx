import { Text, View } from 'react-native';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { UNDO_COPY } from './undoCopy';
import type { VoiceChange } from './voiceChanges';
import {
  voiceChangeChangedId,
  voiceChangeId,
  voiceChangeUndoId,
  voiceChangeWaitingId,
} from './VoiceChangeRow.testIds';

export interface VoiceChangeEntryProps {
  change: VoiceChange;
  onUndo: () => void;
}

// One voice batch in Notes: what it changed, Waiting to send while it hasn't gone, and Undo while it can.
export function VoiceChangeEntry({ change, onUndo }: VoiceChangeEntryProps) {
  const theme = useTheme();
  const { colors } = theme;
  const runner = useActionRunner();
  const { batchId } = change;
  const summary = change.items.map((p) => runner.describe(p.action).text).join(' · ');
  return (
    <View
      testID={voiceChangeId(batchId)}
      style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}
    >
      <View style={{ flex: 1, gap: 2 }}>
        <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
          {UNDO_COPY.rowTitle}
        </Text>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{summary}</Text>
        {change.state === 'waiting' ? (
          <Text testID={voiceChangeWaitingId(batchId)} style={{ color: colors.warn }}>
            {UNDO_COPY.waiting}
          </Text>
        ) : null}
        {change.changedSince ? (
          <Text
            testID={voiceChangeChangedId(batchId)}
            style={{ color: colors.muted, fontSize: theme.font.size.sm }}
          >
            {UNDO_COPY.changedSince}
          </Text>
        ) : null}
      </View>
      {change.undoable ? (
        <Button testID={voiceChangeUndoId(batchId)} variant="ghost" label="Undo" onPress={onUndo} />
      ) : null}
    </View>
  );
}
