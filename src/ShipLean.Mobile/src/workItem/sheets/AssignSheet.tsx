import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { useMembersQuery } from '../queries';
import { assignSheetId, noOwnerOptionId, ownerOptionId, ownerSheetErrorId } from '../testIds';
import { useWorkItemActions } from '../WorkItemActions';

export interface AssignSheetProps {
  item: WorkItemDto;
  open: boolean;
  onClose: () => void;
}

// The project's members by name, plus No owner (the web's Unassigned). Picking one dispatches an assign.
export const AssignSheet: FC<AssignSheetProps> = ({ item, open, onClose }) => {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useWorkItemActions();
  const query = useMembersQuery(item.projectId);
  const members = (query.data ?? []).filter((m) => m.status !== 'disabled');
  const currentId = item.assignee?.id ?? null;

  const pick = (userId: string | null, name: string | null) => {
    onClose();
    if (userId === currentId) return;
    dispatch({
      area: 'workItem',
      kind: 'assign',
      itemId: item.id,
      key: item.key,
      revision: item.revision,
      userId,
      name,
      fromUserId: currentId,
      fromName: item.assignee?.name ?? null,
    });
  };

  const option = (id: string, label: string, userId: string | null, name: string | null) => {
    const current = userId === currentId;
    return (
      <Pressable
        key={id}
        testID={id}
        accessibilityRole="button"
        accessibilityState={{ selected: current }}
        onPress={() => pick(userId, name)}
        style={{
          minHeight: theme.touch.row,
          flexDirection: 'row',
          alignItems: 'center',
          borderBottomWidth: 1,
          borderBottomColor: colors.border,
        }}
      >
        <Text
          style={{
            flex: 1,
            color: userId === null ? colors.muted : colors.text,
            fontSize: theme.font.size.md,
            fontWeight: current ? theme.font.weight.bold : theme.font.weight.regular,
          }}
        >
          {label}
        </Text>
        {current ? (
          <Text style={{ color: colors.accent, fontSize: theme.font.size.md }}>✓</Text>
        ) : null}
      </Pressable>
    );
  };

  return (
    <BottomSheet title="Assign" open={open} onClose={onClose} testID={assignSheetId}>
      {query.isError ? (
        <View testID={ownerSheetErrorId}>
          <ErrorState
            title="Couldn't load the members"
            action={{ label: 'Retry', onPress: () => void query.refetch(), busy: query.isFetching }}
          />
        </View>
      ) : query.data === undefined ? (
        <Skeleton rows={4} />
      ) : (
        <ScrollView style={{ maxHeight: 360 }}>
          {members.map((m) =>
            option(ownerOptionId(m.user.id), m.user.name, m.user.id, m.user.name),
          )}
          {option(noOwnerOptionId, 'No owner', null, null)}
        </ScrollView>
      )}
    </BottomSheet>
  );
};
