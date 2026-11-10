import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { layout, tint } from './layout';
import { primitiveIds } from './testIds';

export interface NeedsPillProps {
  // Questions and decisions waiting on the person; null or both 0 shows nothing.
  needs: { questions: number; decisions: number } | null;
}

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;

// "Needs you" on a row (backlog.html .needs): how many questions and decisions wait on the person.
export function NeedsPill({ needs }: NeedsPillProps) {
  const theme = useTheme();
  if (needs === null) return null;
  const total = needs.questions + needs.decisions;
  if (total === 0) return null;
  const { warn } = theme.colors;
  return (
    <View
      testID={primitiveIds.needsPill}
      accessibilityLabel={`Needs you: ${plural(needs.questions, 'question')}, ${plural(needs.decisions, 'decision')}`}
      style={{
        flexDirection: 'row',
        alignSelf: 'flex-start',
        alignItems: 'center',
        height: layout.pill,
        paddingHorizontal: theme.space[2],
        borderRadius: layout.pill / 2,
        backgroundColor: tint(warn, 0.14),
      }}
    >
      <Text
        style={{ color: warn, fontSize: theme.font.size.sm, fontWeight: theme.font.weight.bold }}
      >
        {total}
      </Text>
    </View>
  );
}
