import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Icon } from './Icon';
import { layout, tint } from './layout';
import { primitiveIds } from './testIds';

export interface NeedsPillProps {
  // Questions and decisions waiting on the person; null or both 0 shows nothing.
  needs: { questions: number; decisions: number } | null;
  // count: the total (default). kinds: check icon and decisions, message icon and questions (Backlog).
  // label: a dot and "Needs you" (Features).
  variant?: 'count' | 'kinds' | 'label';
}

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;

// "Needs you" on a row (backlog.html .needs): how many questions and decisions wait on the person.
export function NeedsPill({ needs, variant = 'count' }: NeedsPillProps) {
  const theme = useTheme();
  if (needs === null) return null;
  const total = needs.questions + needs.decisions;
  if (total === 0) return null;
  const { warn, text } = theme.colors;
  const shell = {
    flexDirection: 'row',
    alignSelf: 'flex-start',
    alignItems: 'center',
    height: layout.pill,
    paddingHorizontal: theme.space[2],
    borderRadius: layout.pill / 2,
    backgroundColor: tint(warn, 0.14),
  } as const;
  const accessibilityLabel = `Needs you: ${plural(needs.questions, 'question')}, ${plural(needs.decisions, 'decision')}`;
  if (variant === 'label') {
    return (
      <View
        testID={primitiveIds.needsPill}
        accessibilityLabel={accessibilityLabel}
        style={{ ...shell, gap: layout.stateGap }}
      >
        <View
          style={{
            width: layout.stateDot,
            height: layout.stateDot,
            borderRadius: layout.stateDot / 2,
            backgroundColor: warn,
          }}
        />
        <Text
          style={{
            color: text,
            fontSize: theme.font.size.sm,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          Needs you
        </Text>
      </View>
    );
  }
  const part = (id: string, icon: 'check' | 'message', n: number) =>
    n === 0 ? null : (
      <View key={id} testID={id} style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
        <Icon name={icon} size={16} color={warn} />
        <Text
          style={{ color: warn, fontSize: theme.font.size.sm, fontWeight: theme.font.weight.bold }}
        >
          {n}
        </Text>
      </View>
    );
  return (
    <View
      testID={primitiveIds.needsPill}
      accessibilityLabel={accessibilityLabel}
      style={{ ...shell, gap: theme.space[2] }}
    >
      {variant === 'kinds' ? (
        <>
          {part(primitiveIds.needsDecisions, 'check', needs.decisions)}
          {part(primitiveIds.needsQuestions, 'message', needs.questions)}
        </>
      ) : (
        <Text
          style={{ color: warn, fontSize: theme.font.size.sm, fontWeight: theme.font.weight.bold }}
        >
          {total}
        </Text>
      )}
    </View>
  );
}
