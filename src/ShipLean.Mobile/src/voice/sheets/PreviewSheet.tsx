import { useContext, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { layout } from '../../ui/layout';
import { VoiceInternalsContext, type VoiceUiStateOf } from '../internals';
import { isRejectedLine, keptLineIds } from '../machine';
import { HeardQuote } from './HeardQuote';
import { heardQuoteId } from './HeardQuote.testIds';
import {
  previewApplyId,
  previewCancelId,
  previewFixId,
  previewLineId,
  previewMarkId,
  previewRemoveId,
  previewSayYesId,
  previewSheetId,
  previewTitleId,
  previewWhyId,
} from './PreviewSheet.testIds';

export interface PreviewSheetProps {
  state: VoiceUiStateOf<'preview'>;
  onApply: (lineIds: string[]) => void;
  onFix: () => void;
  onCancel: () => void;
}

// The changes a command will make, before Apply: the heard (or typed) words, each change as a line that can be
// removed, the parts that can't be done struck out with the reason, then Fix (Edit when typed) and Apply. Nothing
// changes until Apply or "yes". A card over the screen, not a Modal, so the mic can still be held to say "yes".
// No audio is kept: the preview carries the words only.
export function PreviewSheet({ state, onApply, onFix, onCancel }: PreviewSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  // Removed lines live in the voice state, so saying "yes" applies what the sheet shows and a flow that sets the
  // preview again is shown as set. Rendered alone (no provider), the sheet keeps them itself.
  const internals = useContext(VoiceInternalsContext);
  const [localRemoved, setLocalRemoved] = useState(state.removed);
  const removed = internals === null ? localRemoved : state.removed;
  const kept = keptLineIds({ ...state, removed });
  const changes = state.lines.filter((l) => !isRejectedLine(l.id)).length;
  const voice = state.source === 'voice';

  const remove = (id: string) => {
    const next = [...removed, id];
    if (internals === null) setLocalRemoved(next);
    else internals.set({ ...state, removed: next });
  };

  return (
    <View
      testID={previewSheetId}
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
      <Text
        testID={previewTitleId}
        accessibilityRole="header"
        style={{
          color: colors.muted,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {kept.length} change{kept.length === 1 ? '' : 's'} to apply
      </Text>
      <HeardQuote text={state.heard} source={state.source} misses={[]} testID={heardQuoteId} />
      <View style={{ gap: theme.space[2] }}>
        {state.lines
          .filter((l) => !removed.includes(l.id))
          .map((line) => {
            const struck = isRejectedLine(line.id);
            const marks = state.marks.filter((m) => m.lineId === line.id);
            return (
              <View
                key={line.id}
                testID={previewLineId(line.id)}
                accessibilityState={{ disabled: struck }}
                style={{
                  flexDirection: 'row',
                  alignItems: 'flex-start',
                  gap: theme.space[2],
                  padding: theme.space[3],
                  borderRadius: theme.radius.md,
                  backgroundColor: colors.surface2,
                }}
              >
                <View style={{ flex: 1, minWidth: 0, gap: theme.space[1] }}>
                  <Text
                    style={{
                      color: struck ? colors.muted : colors.text,
                      fontSize: theme.font.size.body15,
                      fontWeight: theme.font.weight.semibold,
                      textDecorationLine: struck ? 'line-through' : 'none',
                    }}
                  >
                    {line.text}
                  </Text>
                  {line.detail !== undefined ? (
                    <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
                      {line.detail}
                    </Text>
                  ) : null}
                  {line.warning !== undefined ? (
                    <Text
                      testID={previewWhyId(line.id)}
                      style={{
                        color: struck ? colors.text : colors.warn,
                        fontSize: theme.font.size.sm,
                      }}
                    >
                      {line.warning}
                    </Text>
                  ) : null}
                  {marks.map((m, i) => (
                    <Text
                      key={i}
                      testID={previewMarkId(line.id)}
                      style={{ color: colors.accent, fontSize: theme.font.size.sm }}
                    >
                      {m.note}
                    </Text>
                  ))}
                </View>
                {line.removable && !struck ? (
                  <Pressable
                    testID={previewRemoveId(line.id)}
                    accessibilityRole="button"
                    accessibilityLabel={`Remove ${line.text}`}
                    hitSlop={theme.space[2]}
                    onPress={() => remove(line.id)}
                    style={{
                      minWidth: theme.touch.control,
                      minHeight: theme.touch.control,
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <Text style={{ color: colors.muted, fontSize: theme.font.size.body15 }}>✕</Text>
                  </Pressable>
                ) : null}
              </View>
            );
          })}
      </View>
      <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
        <Button label="Cancel" variant="ghost" onPress={onCancel} testID={previewCancelId} />
        <Button
          label={voice ? 'Fix' : 'Edit'}
          variant="ghost"
          onPress={onFix}
          testID={previewFixId}
        />
        <View style={{ flex: 1 }}>
          <Button
            label={kept.length === changes ? 'Apply' : `Apply ${kept.length}`}
            onPress={() => onApply(kept)}
            disabled={kept.length === 0}
            testID={previewApplyId}
          />
        </View>
      </View>
      {voice ? (
        <Text
          testID={previewSayYesId}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          Or say “yes” · nothing changes until you apply
        </Text>
      ) : null}
    </View>
  );
}
