import { useState } from 'react';
import { Platform, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { engines } from '../engines';
import type { MicStatus } from '../internals';
import { recheckMic } from '../mic/permissionEvents';
import { AppInfoDrawing } from './AppInfoDrawing';
import { micPermissionIds, micPermissionSheetId } from './MicPermissionSheet.testIds';
import { SettingsDrawing } from './SettingsDrawing';

export interface MicPermissionSheetProps {
  open: boolean;
  status: MicStatus;
  onClose: () => void;
  onTypeInstead: () => void;
}

const PROMISES = [
  'Listens only while you hold the button',
  'Turned into text on this phone',
  'Nothing you say is kept; only the text you approve is posted',
];

// Why the mic is off, and the way to turn it on or type instead. The form follows what the phone will still do:
// ask again (the phone's prompt is used only by this button), or point at the exact row in its settings.
export function MicPermissionSheet({
  open,
  status,
  onClose,
  onTypeInstead,
}: MicPermissionSheetProps) {
  const theme = useTheme();
  const [form, setForm] = useState<MicStatus>(status);
  const [seen, setSeen] = useState({ status, open });
  const [asking, setAsking] = useState(false);
  // Opening the sheet again, or a fresh read of the permission, starts from that state.
  if (seen.status !== status || seen.open !== open) {
    setSeen({ status, open });
    setForm(status);
  }

  if (form === 'granted') return null;
  const ask = form === 'undetermined' || form === 'deniedCanAsk';
  const ios = Platform.OS === 'ios';
  const formId = ask
    ? micPermissionIds.formAsk
    : ios
      ? micPermissionIds.formIos
      : micPermissionIds.formAndroid;
  const title = ask
    ? 'Allow the microphone to talk to ShipLean'
    : ios
      ? 'Turn on the microphone in Settings'
      : 'Turn on the microphone in app settings';
  const lead = ask
    ? 'Your phone asks next. You can say no again and keep typing.'
    : ios
      ? 'iPhone won’t ask again, so it’s one switch in Settings.'
      : 'Android won’t ask again. Pick the first option on the Microphone page.';

  const allow = async () => {
    setAsking(true);
    try {
      const yes = await engines().mic.request();
      if (yes) {
        onClose();
        recheckMic();
        return;
      }
      setForm(await engines().mic.status());
    } finally {
      setAsking(false);
    }
  };
  const typeInstead = () => {
    onClose();
    onTypeInstead();
  };
  const goToSettings = () => {
    engines().mic.openSettings();
    onClose();
  };

  return (
    <BottomSheet testID={micPermissionSheetId} title={title} open={open} onClose={onClose}>
      <View testID={formId} style={{ gap: theme.space[3] }}>
        <Text
          testID={micPermissionIds.lead}
          style={{ color: theme.colors.muted, fontSize: theme.font.size.body15 }}
        >
          {lead}
        </Text>
        {ask ? (
          <View testID={micPermissionIds.promises} style={{ gap: theme.space[2] }}>
            {PROMISES.map((p) => (
              <Text
                key={p}
                style={{ color: theme.colors.text, fontSize: theme.font.size.body15 }}
              >{`✓  ${p}`}</Text>
            ))}
          </View>
        ) : ios ? (
          <SettingsDrawing />
        ) : (
          <AppInfoDrawing />
        )}
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <View style={{ flex: 1 }}>
            <Button
              testID={micPermissionIds.typeInstead}
              label="Type instead"
              variant="ghost"
              onPress={typeInstead}
            />
          </View>
          <View style={{ flex: 1 }}>
            {ask ? (
              <Button
                testID={micPermissionIds.allow}
                label="Allow microphone"
                busy={asking}
                onPress={() => void allow()}
              />
            ) : (
              <Button
                testID={ios ? micPermissionIds.openSettings : micPermissionIds.appSettings}
                label={ios ? 'Open Settings' : 'App settings'}
                onPress={goToSettings}
              />
            )}
          </View>
        </View>
        <Text
          testID={micPermissionIds.note}
          style={{
            color: theme.colors.muted,
            fontSize: theme.font.size.sm,
            textAlign: 'center',
          }}
        >
          Everything else works without voice
        </Text>
      </View>
    </BottomSheet>
  );
}
