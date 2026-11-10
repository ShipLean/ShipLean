import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import {
  newStatusId,
  statusLineId,
  statusTextId,
  statusVerifiedById,
  statusVerifiedId,
  stillTrueId,
} from '../testIds';

export interface StatusLineProps {
  item: WorkItemDto;
  onStillTrue: () => void;
  onNewStatus?: () => void;
}

// A Feature's status line and who verified it, worded as the web's VerifiedChip does. The server says when it is stale.
export const StatusLine: FC<StatusLineProps> = ({ item, onStillTrue, onNewStatus }) => {
  const theme = useTheme();
  const { colors } = theme;
  const verified = item.statusVerified;
  const link = (id: string, label: string, onPress: () => void) => (
    <Pressable
      testID={id}
      accessibilityRole="link"
      accessibilityLabel={label}
      onPress={onPress}
      style={{
        minHeight: theme.touch.control,
        minWidth: theme.touch.control,
        justifyContent: 'center',
      }}
    >
      <Text
        style={{
          color: colors.accent,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
  return (
    <View
      testID={statusLineId}
      style={{
        margin: theme.space[4],
        padding: theme.space[3],
        gap: theme.space[1],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Text testID={statusTextId} style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
        {item.statusLine === '' ? 'No status yet.' : item.statusLine}
      </Text>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: theme.space[2],
        }}
      >
        {verified === null ? (
          <Text
            testID={statusVerifiedId}
            style={{ color: colors.muted, fontSize: theme.font.size.sm }}
          >
            Not verified yet
          </Text>
        ) : (
          <>
            <Text
              testID={statusVerifiedId}
              style={{
                color: verified.stale ? colors.warn : colors.ok,
                fontSize: theme.font.size.sm,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {verified.stale ? '⚠' : '✓'} Verified {verified.atLabel}
            </Text>
            <Text
              testID={statusVerifiedById}
              style={{ color: colors.muted, fontSize: theme.font.size.sm }}
            >
              {verified.agent === null ? (verified.by?.name ?? '') : `${verified.agent} (agent)`}
              {verified.via === null ? '' : ` through ${verified.via}`}
            </Text>
          </>
        )}
      </View>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[4] }}>
        {link(stillTrueId, 'Still true', onStillTrue)}
        {onNewStatus === undefined ? null : link(newStatusId, 'New status', onNewStatus)}
      </View>
    </View>
  );
};
