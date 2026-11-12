import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { engines } from '../../voice/engines';
import {
  inputCardId,
  inputCardNameId,
  inputChangeId,
  inputOptionId,
  inputSheetDoneId,
  inputSheetId,
} from './InputCard.testIds';
import { inputDetail, inputTitle } from './input/inputLabels';
import { useVoiceInputs } from './input/useVoiceInputs';

// The input voice will record from (the pick when it is connected, else the phone), and a sheet to pick another.
export function InputCard() {
  const theme = useTheme();
  const { colors } = theme;
  const { inputs, picked, inUse } = useVoiceInputs();
  const [open, setOpen] = useState(false);
  return (
    <View
      testID={inputCardId}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[3],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <View style={{ flex: 1 }}>
        <Text
          testID={inputCardNameId}
          style={{
            color: colors.text,
            fontSize: theme.font.size.md,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {inputTitle(inUse)}
        </Text>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
          {inUse.kind === 'phone'
            ? "The phone's own microphone and loudspeaker"
            : inputDetail(inUse)}
        </Text>
      </View>
      <Button testID={inputChangeId} label="Change" variant="ghost" onPress={() => setOpen(true)} />
      <BottomSheet
        title="Listen through"
        open={open}
        onClose={() => setOpen(false)}
        testID={inputSheetId}
      >
        <View style={{ paddingVertical: theme.space[2] }}>
          {inputs.map((i) => (
            <Pressable
              key={i.id}
              testID={inputOptionId(i.id)}
              accessibilityRole="radio"
              accessibilityState={{ selected: i.id === picked, disabled: !i.connected }}
              disabled={!i.connected}
              onPress={() => {
                engines().tuning.pick(i.id);
                setOpen(false);
              }}
              style={{
                minHeight: theme.touch.row,
                flexDirection: 'row',
                alignItems: 'center',
                opacity: i.connected ? 1 : 0.5,
                borderBottomWidth: 1,
                borderBottomColor: colors.border,
              }}
            >
              <View style={{ flex: 1 }}>
                <Text style={{ color: colors.text, fontSize: theme.font.size.md }}>
                  {inputTitle(i)}
                </Text>
                <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                  {inputDetail(i)}
                </Text>
              </View>
              <Text style={{ color: colors.accent }}>{i.id === inUse.id ? '●' : '○'}</Text>
            </Pressable>
          ))}
          <Text style={{ color: colors.muted, paddingVertical: theme.space[2] }}>
            Each input keeps its own sensitivity. Run the test after you change it.
          </Text>
          <Button testID={inputSheetDoneId} label="Done" onPress={() => setOpen(false)} />
        </View>
      </BottomSheet>
    </View>
  );
}
