import { Text, View } from 'react-native';
import { usePending } from '../../actions/ActionRunnerProvider';
import type { Pending, PreviewLine } from '../../actions/types';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { layout } from '../../ui/layout';
import { applyGroup, type ApplyGroup } from '../flows/applyFailed';
import type { VoiceUiStateOf } from '../internals';
import { HeardQuote } from './HeardQuote';
import { heardQuoteId } from './HeardQuote.testIds';
import {
  applyFailedDiscardId,
  applyFailedGroupId,
  applyFailedHeadingId,
  applyFailedLineId,
  applyFailedMayHaveLandedId,
  applyFailedQueueId,
  applyFailedReasonId,
  applyFailedRetryId,
  applyFailedSheetId,
} from './ApplyFailedSheet.testIds';

export interface ApplyFailedSheetProps {
  state: VoiceUiStateOf<'applyFailed'>;
  onRetry: () => void;
  onQueue: () => void;
  onDiscard: () => void;
}

const GROUPS: { group: ApplyGroup; title: string }[] = [
  { group: 'stillApplied', title: 'Still applied' },
  { group: 'reversed', title: 'Reversed' },
  { group: 'notSent', title: 'Not sent' },
];

// An applied voice batch that failed: what stayed applied, what was reversed and what wasn't sent, read live from the
// runner (it may still be reversing). "Nothing was changed" only when nothing stayed applied. Then the reason, and
// Try again and Send when online when sending again can help; Discard sends nothing more.
export function ApplyFailedSheet({ state, onRetry, onQueue, onDiscard }: ApplyFailedSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const parts = usePending((p) => p.batchId === state.batchId);
  // lines[i] is parts[i]: onApplyFailed builds one line per part, in the runner's order.
  const rows = state.lines.map((line, i) => ({ line, part: parts[i] as Pending | undefined }));
  const groupOf = (part: Pending | undefined): ApplyGroup =>
    part === undefined ? 'notSent' : applyGroup(part);
  const stayed = rows.some((r) => groupOf(r.part) === 'stillApplied');

  return (
    <View
      testID={applyFailedSheetId}
      style={{
        position: 'absolute',
        left: theme.space[4],
        right: theme.space[4],
        bottom: layout.tabBar + layout.dockMic + theme.space[5],
        padding: theme.space[4],
        gap: theme.space[3],
        borderRadius: layout.sheetRadius,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
      }}
    >
      <View
        style={{
          gap: theme.space[1],
          padding: theme.space[3],
          borderRadius: theme.radius.md,
          backgroundColor: colors.surface2,
          borderLeftWidth: 3,
          borderLeftColor: colors.danger,
        }}
      >
        <Text
          testID={applyFailedHeadingId}
          accessibilityRole="header"
          style={{
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {stayed ? 'Some changes stayed applied' : 'Nothing was changed'}
        </Text>
        <Text
          testID={applyFailedReasonId}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {state.reason}
        </Text>
      </View>
      <HeardQuote text={state.heard} source={state.source} misses={[]} testID={heardQuoteId} />
      {GROUPS.map(({ group, title }) => {
        const inGroup = rows.filter((r) => groupOf(r.part) === group);
        if (inGroup.length === 0) return null;
        return (
          <View key={group} testID={applyFailedGroupId(group)} style={{ gap: theme.space[2] }}>
            <Text
              accessibilityRole="header"
              style={{
                color: colors.muted,
                fontSize: theme.font.size.sm,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {title}
            </Text>
            {inGroup.map(({ line, part }) => (
              <Row key={line.id} line={line} uncertain={part?.state === 'uncertain'} />
            ))}
          </View>
        );
      })}
      {state.retryable ? (
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <Button
            label="Send when online"
            variant="ghost"
            onPress={onQueue}
            testID={applyFailedQueueId}
          />
          <View style={{ flex: 1 }}>
            <Button label="Try again" onPress={onRetry} testID={applyFailedRetryId} />
          </View>
        </View>
      ) : null}
      <Button label="Discard" variant="ghost" onPress={onDiscard} testID={applyFailedDiscardId} />
    </View>
  );
}

function Row({ line, uncertain }: { line: PreviewLine; uncertain: boolean }) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={applyFailedLineId(line.id)}
      style={{
        gap: theme.space[1],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        backgroundColor: colors.surface2,
      }}
    >
      <Text
        style={{
          color: colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {line.text}
      </Text>
      {line.detail !== undefined ? (
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{line.detail}</Text>
      ) : null}
      {uncertain ? (
        <Text
          testID={applyFailedMayHaveLandedId(line.id)}
          style={{ color: colors.warn, fontSize: theme.font.size.sm }}
        >
          No answer: it may have landed
        </Text>
      ) : null}
    </View>
  );
}
