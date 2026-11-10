import { useEffect, useState } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { layout } from '../ui/layout';
import { Toast } from '../ui/Toast';
import { UNDO_COPY } from '../voice/after/undoCopy';
import { UndoPartialSheet } from '../voice/after/UndoPartialSheet';
import { useActionRunner } from './ActionRunnerProvider';
import type { Pending, UndoOutcome } from './types';
import { undoneToastId, undoToastId } from './UndoToastHost.testIds';
import { useUndoToast } from './useUndoToast';

// What the toast's Undo came to: everything undone says so; anything else opens the partial sheet.
type Result =
  | { kind: 'undone'; key: string | null }
  | { kind: 'partial'; batchId: string; items: Pending[]; outcomes: UndoOutcome[] };

// How long "Undone" stays up.
const UNDONE_MS = 4000;

// The runner's undo toast: the newest batch whose undo window is open, with Undo. It goes when the window closes.
// After Undo (any origin): every outcome undone shows "Undone"; otherwise the partial sheet lists what stayed.
export function UndoToastHost() {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const runner = useActionRunner();
  const toast = useUndoToast();
  const [dismissed, setDismissed] = useState<string | null>(null);
  const [result, setResult] = useState<Result | null>(null);

  const undo = async (batchId: string, items: Pending[], run: () => Promise<UndoOutcome[]>) => {
    const outcomes = await run();
    if (outcomes.every((o) => o.outcome === 'undone')) {
      const keys = new Set(items.map((p) => p.action.key));
      setResult({ kind: 'undone', key: keys.size === 1 ? [...keys][0] : null });
    } else setResult({ kind: 'partial', batchId, items, outcomes });
  };

  const undoneShown = result?.kind === 'undone';
  useEffect(() => {
    if (!undoneShown) return;
    const timer = setTimeout(() => setResult(null), UNDONE_MS);
    return () => clearTimeout(timer);
  }, [undoneShown]);

  const showToast = toast !== null && toast.batchId !== dismissed;
  return (
    <>
      {showToast || undoneShown ? (
        <View
          testID={showToast ? undoToastId : undoneToastId}
          pointerEvents="box-none"
          style={{
            position: 'absolute',
            left: theme.space[4],
            right: theme.space[4],
            bottom: insets.bottom + layout.tabBar + layout.toastGap,
          }}
        >
          {showToast ? (
            <Toast
              text={toast.text}
              action={{
                label: 'Undo',
                onPress: () => {
                  // The batch as it is now: an undone action may leave pending().
                  const items = runner.pending().filter((p) => p.batchId === toast.batchId);
                  void undo(toast.batchId, items, toast.undo);
                },
              }}
              onDismiss={() => setDismissed(toast.batchId)}
            />
          ) : (
            <Toast
              text={UNDO_COPY.undone(result?.kind === 'undone' ? result.key : null)}
              onDismiss={() => setResult(null)}
            />
          )}
        </View>
      ) : null}
      {result?.kind === 'partial' ? (
        <UndoPartialSheet
          items={result.items}
          outcomes={result.outcomes}
          onRetry={() => void undo(result.batchId, result.items, () => runner.undo(result.batchId))}
          onClose={() => setResult(null)}
        />
      ) : null}
    </>
  );
}
