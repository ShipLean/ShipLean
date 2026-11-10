import type { FC, ReactNode } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';

export interface SectionCardProps {
  title: string;
  children: ReactNode;
}

// A titled card of the Work item screen (app.css .section-h and .card).
export const SectionCard: FC<SectionCardProps> = ({ title, children }) => {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View style={{ marginHorizontal: theme.space[4], marginTop: theme.space[3] }}>
      <Text
        accessibilityRole="header"
        style={{
          color: colors.muted,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
          marginBottom: theme.space[2],
        }}
      >
        {title}
      </Text>
      <View
        style={{
          padding: theme.space[3],
          gap: theme.space[2],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surface,
        }}
      >
        {children}
      </View>
    </View>
  );
};
