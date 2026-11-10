import type { UserRef } from '@shiplean/core/api/refs';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';

export interface FaceProps {
  user: UserRef;
  size: number;
  testID?: string;
}

// A person's initials on their colour (app.css .avatar).
export function Face({ user, size, testID }: FaceProps) {
  const theme = useTheme();
  return (
    <View
      testID={testID}
      accessibilityLabel={user.name}
      style={{
        width: size,
        height: size,
        borderRadius: size / 2,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: user.avatarColour,
        borderWidth: 2,
        borderColor: theme.colors.surface,
      }}
    >
      <Text
        style={{
          color: '#ffffff',
          fontSize: Math.round(size * 0.42),
          fontWeight: theme.font.weight.bold,
        }}
      >
        {user.initials}
      </Text>
    </View>
  );
}
