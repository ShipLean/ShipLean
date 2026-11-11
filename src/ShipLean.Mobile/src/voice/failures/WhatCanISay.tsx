import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import type { ScreenVoiceSpec } from '../contracts';
import { WHAT_CAN_I_SAY } from './correctionCopy';
import {
  whatCanISayActionId,
  whatCanISayDoneId,
  whatCanISayEmptyId,
  whatCanISayId,
  whatCanISaySheetId,
  whatCanISayWebOnlyId,
} from './WhatCanISay.testIds';

export interface WhatCanISayProps {
  spec: ScreenVoiceSpec | null;
  onClose: () => void;
}

// The commands the focused screen understands (voice D11): each action as a line to read, and the web-only ones
// with where they're done.
export function WhatCanISay({ spec, onClose }: WhatCanISayProps) {
  const theme = useTheme();
  const { colors } = theme;
  const actions = spec?.actions ?? [];
  const phone = actions.filter((a) => a.webOnly === undefined);
  const web = actions.filter((a) => a.webOnly !== undefined);
  return (
    <View testID={whatCanISayId}>
      <BottomSheet title={WHAT_CAN_I_SAY.title} open onClose={onClose} testID={whatCanISaySheetId}>
        <View style={{ gap: theme.space[3] }}>
          {actions.length === 0 ? (
            <Text
              testID={whatCanISayEmptyId}
              style={{ color: colors.muted, fontSize: theme.font.size.body15 }}
            >
              {WHAT_CAN_I_SAY.empty}
            </Text>
          ) : null}
          {phone.map((a) => (
            <View key={a.name} testID={whatCanISayActionId(a.name)}>
              <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
                {a.description}
              </Text>
            </View>
          ))}
          {web.length === 0 ? null : (
            <Text
              style={{
                color: colors.muted,
                fontSize: theme.font.size.xs,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {WHAT_CAN_I_SAY.webOnlyHeading}
            </Text>
          )}
          {web.map((a) => (
            <View key={a.name} testID={whatCanISayWebOnlyId(a.name)}>
              <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
                {a.description}
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{a.webOnly}</Text>
            </View>
          ))}
          <Button testID={whatCanISayDoneId} label={WHAT_CAN_I_SAY.done} onPress={onClose} />
        </View>
      </BottomSheet>
    </View>
  );
}
