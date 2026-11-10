import { useState } from 'react';
import { Text, View } from 'react-native';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import type { Pending, UndoOutcome } from '../../actions/types';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { ChangeLine } from './ChangeLine';
import { UNDO_COPY } from './undoCopy';
import { UndoPartialSheet } from './UndoPartialSheet';
import {
  undoPreviewCancelId,
  undoPreviewConfirmId,
  undoPreviewDoneId,
  undoPreviewFailedId,
  undoPreviewLineId,
  undoPreviewRetryId,
  undoPreviewSheetId,
  undoPreviewUndoneId,
} from './UndoPreviewSheet.testIds';

export interface UndoPreviewSheetProps {
  batchId: string;
  itemKey: string;
  // The batch's actions, as the row showed them.
  items: Pending[];
  onClose: () => void;
}

// Undo after the toast (voice D20): what will be undone, previewed like any change; only Undo here runs it. Every
// outcome undone says Undone, with Done; none reached ShipLean leaves Try again; anything else shows what stayed.
export function UndoPreviewSheet({ batchId, itemKey, items, onClose }: UndoPreviewSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const runner = useActionRunner();
  const [busy, setBusy] = useState(false);
  const [outcomes, setOutcomes] = useState<UndoOutcome[] | null>(null);

  const run = async () => {
    setBusy(true);
    const result = await runner.undo(batchId);
    setBusy(false);
    setOutcomes(result);
  };

  const undone = outcomes !== null && outcomes.every((o) => o.outcome === 'undone');
  const failed = outcomes !== null && outcomes.every((o) => o.outcome === 'failed');
  if (undone)
    return (
      <BottomSheet
        title={UNDO_COPY.undoneHeading}
        open
        onClose={onClose}
        testID={undoPreviewSheetId}
      >
        <View style={{ gap: theme.space[3] }}>
          <Text
            testID={undoPreviewUndoneId}
            style={{ color: colors.text, fontSize: theme.font.size.body15 }}
          >
            {UNDO_COPY.undone(itemKey)}
          </Text>
          <Button testID={undoPreviewDoneId} label="Done" onPress={onClose} />
        </View>
      </BottomSheet>
    );
  if (outcomes !== null && !failed)
    return (
      <UndoPartialSheet
        items={items}
        outcomes={outcomes}
        onRetry={() => void run()}
        onClose={onClose}
      />
    );

  return (
    <BottomSheet
      title={UNDO_COPY.previewTitle(items.length)}
      open
      onClose={onClose}
      testID={undoPreviewSheetId}
    >
      <View style={{ gap: theme.space[3] }}>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{itemKey}</Text>
        {items.map((p) => (
          <ChangeLine
            key={p.id}
            testID={undoPreviewLineId(p.id)}
            text={runner.describe(p.action).text}
          />
        ))}
        {failed ? (
          <View testID={undoPreviewFailedId} style={{ gap: 2 }}>
            <Text style={{ color: colors.danger, fontSize: theme.font.size.body15 }}>
              {UNDO_COPY.failedTitle}
            </Text>
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
              {UNDO_COPY.failedDetail}
            </Text>
          </View>
        ) : null}
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <Button testID={undoPreviewCancelId} variant="ghost" label="Cancel" onPress={onClose} />
          <Button
            testID={failed ? undoPreviewRetryId : undoPreviewConfirmId}
            label={failed ? 'Try again' : 'Undo'}
            busy={busy}
            onPress={() => void run()}
          />
        </View>
      </View>
    </BottomSheet>
  );
}
