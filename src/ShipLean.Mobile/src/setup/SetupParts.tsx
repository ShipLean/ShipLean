// The pieces the three setup steps share: the page body, a heading, a list of ticked lines and the buttons at the
// foot (the Screen keeps clear of the home indicator). Touch only: 15 px body text and 44 px targets, in both themes.
import type { ReactNode } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useTheme } from '../theme';

export function StepBody({ children, testID }: { children: ReactNode; testID: string }) {
  const theme = useTheme();
  return (
    <View testID={testID} style={{ flex: 1 }}>
      <ScrollView
        contentContainerStyle={{ padding: theme.space[4], gap: theme.space[3] }}
        keyboardShouldPersistTaps="handled"
      >
        {children}
      </ScrollView>
    </View>
  );
}

export function StepFoot({ children }: { children: ReactNode }) {
  const theme = useTheme();
  return (
    <View
      style={{
        gap: theme.space[2],
        padding: theme.space[4],
        borderTopWidth: 1,
        borderTopColor: theme.colors.border,
        backgroundColor: theme.colors.surface,
      }}
    >
      {children}
    </View>
  );
}

export function Heading({ children }: { children: string }) {
  const theme = useTheme();
  return (
    <Text
      accessibilityRole="header"
      style={{
        color: theme.colors.text,
        fontSize: theme.font.size.lg,
        fontWeight: theme.font.weight.bold,
      }}
    >
      {children}
    </Text>
  );
}

export function Body({
  children,
  muted,
  testID,
}: {
  children: ReactNode;
  muted?: boolean;
  testID?: string;
}) {
  const theme = useTheme();
  return (
    <Text
      testID={testID}
      style={{
        color: muted === true ? theme.colors.muted : theme.colors.text,
        fontSize: theme.font.size.body15,
      }}
    >
      {children}
    </Text>
  );
}

export function GroupHeading({ children }: { children: string }) {
  const theme = useTheme();
  return (
    <Text
      style={{
        marginTop: theme.space[2],
        color: theme.colors.muted,
        fontSize: theme.font.size.sm,
        fontWeight: theme.font.weight.semibold,
        textTransform: 'uppercase',
      }}
    >
      {children}
    </Text>
  );
}

export interface TickedLine {
  id: string;
  title: string;
  body?: string;
}

// The promises and the reasons, each with a tick.
export function TickedList({ lines, testID }: { lines: TickedLine[]; testID: string }) {
  const theme = useTheme();
  return (
    <View testID={testID} style={{ gap: theme.space[3] }}>
      {lines.map((line) => (
        <View
          key={line.id}
          testID={`${testID}-${line.id}`}
          style={{ flexDirection: 'row', gap: theme.space[2] }}
        >
          <Text style={{ color: theme.colors.ok, fontSize: theme.font.size.body15 }}>✓</Text>
          <View style={{ flex: 1, gap: theme.space[1] }}>
            <Text style={{ color: theme.colors.text, fontSize: theme.font.size.body15 }}>
              {line.title}
            </Text>
            {line.body === undefined ? null : (
              <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
                {line.body}
              </Text>
            )}
          </View>
        </View>
      ))}
    </View>
  );
}

export function ProgressLine({
  fraction,
  testID,
  tone = 'accent',
  height = 6,
}: {
  fraction: number;
  testID: string;
  tone?: 'accent' | 'danger' | 'ok';
  height?: number;
}) {
  const theme = useTheme();
  const clamped = Math.max(0, Math.min(1, fraction));
  return (
    <View
      testID={testID}
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: 100, now: Math.round(clamped * 100) }}
      style={{
        height,
        borderRadius: theme.radius.xs,
        overflow: 'hidden',
        backgroundColor: theme.colors.surface2,
      }}
    >
      <View
        style={{
          width: `${clamped * 100}%`,
          height: '100%',
          backgroundColor: theme.colors[tone],
        }}
      />
    </View>
  );
}
