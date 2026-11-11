import { useState, type FunctionComponent } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { BottomSheet } from '../ui/BottomSheet';
import { Button } from '../ui/Button';
import type { ModelChoice } from '../voice/contracts';
import { engines } from '../voice/engines';
import { formatGb, modelChoiceView } from './modelChoice';
import { changeModelIds, changeModelOptionId } from './ChangeModelSheet.testIds';
import { useModelStatus } from './useModelStatus';
import { changeSummary } from './voiceSettingsView';

export interface ChangeModelSheetProps {
  onClose: () => void;
}

interface Choice {
  id: ModelChoice;
  name: string;
  note: string | null;
  disabled: boolean;
}

// Mounted while it is open, so each opening starts from the current choice. The same three choices as setup, with this phone's rules. Picking another model says what downloads, what voice
// keeps using until it is checked and what is then freed; Download calls choose and the store does the rest.
export const ChangeModelSheet: FunctionComponent<ChangeModelSheetProps> = ({ onClose }) => {
  const theme = useTheme();
  const { colors } = theme;
  const store = engines().models;
  const status = useModelStatus();
  const view = modelChoiceView(store.deviceMemory());
  const [pick, setPick] = useState<ModelChoice>(status.choice);

  const choices: Choice[] = [
    ...(view.auto === null
      ? []
      : [{ id: 'auto' as const, name: 'Auto', note: view.auto.line, disabled: false }]),
    ...view.commandRows.map((row) => ({
      id: row.id,
      name: `${row.name} · ${row.tag} · ${formatGb(row.bytes)}`,
      note: row.blockedReason ?? row.warning,
      disabled: row.availability === 'blocked',
    })),
  ];
  const summary = changeSummary(status, view, pick);
  const download = () => {
    store.choose(pick);
    // choose() starts a download only when one is already wanted: from off the screen asks for it.
    if (status.phase === 'none' || status.phase === 'skipped') store.download();
    onClose();
  };

  return (
    <BottomSheet title="Command model" open onClose={onClose} testID={changeModelIds.root}>
      <View style={{ gap: theme.space[2], paddingVertical: theme.space[2] }}>
        {choices.map((c) => {
          const selected = pick === c.id;
          return (
            <Pressable
              key={c.id}
              testID={changeModelOptionId(c.id)}
              accessibilityRole="radio"
              accessibilityLabel={c.name}
              accessibilityState={{ selected, disabled: c.disabled }}
              disabled={c.disabled}
              onPress={() => setPick(c.id)}
              style={{
                minHeight: theme.touch.row,
                padding: theme.space[3],
                gap: theme.space[1],
                borderRadius: theme.radius.md,
                borderWidth: 1,
                borderColor: selected ? colors.accent : colors.border,
                backgroundColor: selected ? colors.accentSoft : colors.surface,
                opacity: c.disabled ? 0.55 : 1,
              }}
            >
              <Text
                style={{
                  color: colors.text,
                  fontSize: theme.font.size.body15,
                  fontWeight: theme.font.weight.semibold,
                }}
              >
                {c.name}
              </Text>
              {c.note === null ? null : (
                <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                  {c.note}
                </Text>
              )}
            </Pressable>
          );
        })}
        <View testID={changeModelIds.summary} style={{ gap: theme.space[1] }}>
          {summary.lines.map((line) => (
            <Text key={line} style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
              {line}
            </Text>
          ))}
        </View>
        <Button testID={changeModelIds.cancel} label="Cancel" variant="ghost" onPress={onClose} />
        <Button
          testID={changeModelIds.download}
          label={summary.downloadLabel ?? 'Download'}
          disabled={summary.downloadLabel === null}
          onPress={download}
        />
      </View>
    </BottomSheet>
  );
};
