import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import { useState, type FC } from 'react';
import { View } from 'react-native';
import { links } from '../../links';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { AssignSheet } from '../sheets/AssignSheet';
import { ChangeStateSheet } from '../sheets/ChangeStateSheet';
import { assignButtonId, childrenLinkId, changeStateButtonId, itemActionsRowId } from '../testIds';

export interface ItemActionsRowProps {
  item: WorkItemDto;
  projectKey: string;
}

// Under the header: Change state, Assign, and on an Epic or Feature the count of what sits under it, which opens
// the Backlog filtered to it (a Feature's stories, an Epic's features). Every change is a dispatch from the sheets.
export const ItemActionsRow: FC<ItemActionsRowProps> = ({ item, projectKey }) => {
  const theme = useTheme();
  const router = useRouter();
  const [stateOpen, setStateOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);

  const below =
    item.type.id === 'feature'
      ? { child: 'story', one: 'story', many: 'stories' }
      : item.type.id === 'epic'
        ? { child: 'feature', one: 'feature', many: 'features' }
        : null;
  const count = below === null ? 0 : item.children.filter((c) => c.type.id === below.child).length;

  const open = () => {
    if (below === null) return;
    router.push(
      item.type.id === 'feature'
        ? links.backlog(projectKey, { level: 'stories', feature: item.key })
        : links.backlog(projectKey, { level: 'features', epic: item.key }),
    );
  };

  return (
    <View
      testID={itemActionsRowId}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        paddingVertical: theme.space[2],
      }}
    >
      <Button
        label="Change state"
        variant="ghost"
        testID={changeStateButtonId}
        onPress={() => setStateOpen(true)}
      />
      <Button
        label="Assign"
        variant="ghost"
        testID={assignButtonId}
        onPress={() => setAssignOpen(true)}
      />
      {below === null ? null : (
        <Button
          label={`${count} ${count === 1 ? below.one : below.many} ›`}
          variant="ghost"
          testID={childrenLinkId}
          onPress={open}
        />
      )}
      <ChangeStateSheet item={item} open={stateOpen} onClose={() => setStateOpen(false)} />
      <AssignSheet item={item} open={assignOpen} onClose={() => setAssignOpen(false)} />
    </View>
  );
};
