import type { UserRef } from '@shiplean/core/api/refs';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { primitiveIds } from './testIds';

export interface AvatarProps {
  user: UserRef;
  // An agent shows "AI" on the info colour instead of the person's initials.
  agent?: boolean;
  size?: 'sm' | 'md';
}

// app.css .avatar: 28 round, 22 when small; the person's initials on their colour.
const SIZES = { md: { box: 28, text: 11 }, sm: { box: 22, text: 9.5 } } as const;

export function Avatar({ user, agent = false, size = 'md' }: AvatarProps) {
  const { colors, font } = useTheme();
  const { box, text } = SIZES[size];
  return (
    <View
      testID={primitiveIds.avatar}
      accessibilityLabel={agent ? 'AI agent' : user.name}
      style={{
        width: box,
        height: box,
        borderRadius: box / 2,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: agent ? colors.info : user.avatarColour,
      }}
    >
      <Text style={{ color: colors.accentFg, fontSize: text, fontWeight: font.weight.semibold }}>
        {agent ? 'AI' : user.initials}
      </Text>
    </View>
  );
}
