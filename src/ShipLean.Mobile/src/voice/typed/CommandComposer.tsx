import { Text, TextInput, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { useVoice } from '../VoiceProvider';
import { commandMode } from './commandMode';
import type { ComposerReason } from './composerRequest';
import {
  commandComposerFieldId,
  commandComposerGreyedId,
  commandComposerId,
  commandComposerPreviewId,
  commandComposerReasonId,
  commandComposerRouteId,
} from './CommandComposer.testIds';
import { reasonLine } from './reasonLine';

export interface CommandComposerProps {
  text: string;
  onChangeText: (text: string) => void;
  // Why the composer opened: the mic is off, the mic check failed, or a command is being edited.
  reason: ComposerReason;
  // Called after the command is sent.
  onSent: () => void;
}

// The dock's Command mode: a typed command goes through the same preview as a spoken one (typed D5). Preview hands
// the text to the voice preview and nothing changes here. Greyed with the reason while the models aren't there
// (typed D8). The field takes focus when it mounts, so switching from Note keeps the keyboard up.
export function CommandComposer({ text, onChangeText, reason, onSent }: CommandComposerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const voice = useVoice();
  const mode = commandMode(voice.state);
  const why = reasonLine(reason);
  const blank = text.trim() === '';
  const preview = () => {
    voice.submitTyped(text);
    onSent();
  };
  return (
    <View testID={commandComposerId} style={{ gap: theme.space[2] }}>
      {mode.greyed !== null ? (
        <Text
          testID={commandComposerGreyedId}
          style={{ color: colors.muted, fontSize: theme.font.size.xs }}
        >
          {mode.greyed}
        </Text>
      ) : null}
      <TextInput
        testID={commandComposerFieldId}
        accessibilityLabel="Command"
        autoFocus
        multiline
        value={text}
        onChangeText={onChangeText}
        placeholder="Say what to change, as you would aloud"
        placeholderTextColor={colors.muted}
        style={{
          minHeight: theme.touch.control,
          paddingHorizontal: theme.space[3],
          paddingVertical: theme.space[2],
          borderRadius: theme.radius.lg,
          borderWidth: 1.5,
          borderColor: colors.accent,
          backgroundColor: colors.surface,
          color: colors.text,
          fontSize: theme.font.size.input,
        }}
      />
      {why !== null ? (
        <Text
          testID={commandComposerReasonId}
          style={{ color: colors.muted, fontSize: theme.font.size.xs }}
        >
          {why}
        </Text>
      ) : null}
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[3] }}>
        <Text
          testID={commandComposerRouteId}
          style={{ flex: 1, minWidth: 0, color: colors.muted, fontSize: theme.font.size.sm }}
        >
          <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
            {mode.route.lead}
          </Text>
          {mode.route.rest}
        </Text>
        <Button
          testID={commandComposerPreviewId}
          label={mode.send}
          onPress={preview}
          disabled={blank || mode.greyed !== null}
        />
      </View>
    </View>
  );
}
