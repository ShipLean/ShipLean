import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import type { AnswerSheetContent } from '../contracts';
import { answerDoneId, answerLeadId, answerRowId, answerSheetId } from './AnswerSheet.testIds';

export interface AnswerSheetProps {
  content: AnswerSheetContent;
  onClose: () => void;
}

// A question's answer, shown on screen (never read aloud): the lead line, the rows (key, title, counts), and Done.
// Nothing to apply. On screen only: no read-aloud, TTS or sound; copy never promises anyone is notified.
export function AnswerSheet({ content, onClose }: AnswerSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={answerSheetId}
      style={{
        position: 'absolute',
        left: 0,
        right: 0,
        bottom: 0,
        padding: theme.space[4],
        gap: theme.space[3],
        borderTopLeftRadius: theme.radius.lg,
        borderTopRightRadius: theme.radius.lg,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
      }}
    >
      <Text
        testID={answerLeadId}
        accessibilityRole="header"
        style={{
          color: colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {content.lead}
      </Text>
      {content.rows.map((row) => (
        <View
          key={row.key}
          testID={answerRowId(row.key)}
          style={{ flexDirection: 'row', gap: theme.space[2], alignItems: 'baseline' }}
        >
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{row.key}</Text>
          <Text
            numberOfLines={1}
            style={{ flex: 1, minWidth: 0, color: colors.text, fontSize: theme.font.size.body15 }}
          >
            {row.title}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{row.counts}</Text>
        </View>
      ))}
      <Button label="Done" onPress={onClose} testID={answerDoneId} />
    </View>
  );
}
