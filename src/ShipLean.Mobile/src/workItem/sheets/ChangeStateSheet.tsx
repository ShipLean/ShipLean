import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { useTypeStatesQuery } from '../queries';
import { changeStateSheetId, stateOptionId, stateSheetErrorId } from '../testIds';
import { useWorkItemActions } from '../WorkItemActions';

export interface ChangeStateSheetProps {
  item: WorkItemDto;
  open: boolean;
  onClose: () => void;
}

// Every state of the item's type, by order, the current one ticked (P-W2: the server has no transition rules).
// Picking one dispatches a setState; only the executor talks to the server.
export const ChangeStateSheet: FC<ChangeStateSheetProps> = ({ item, open, onClose }) => {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useWorkItemActions();
  const query = useTypeStatesQuery(item.projectId, item.type.id);
  const states = [...(query.data ?? [])].sort((a, b) => a.order - b.order);

  const pick = (state: (typeof states)[number]) => {
    onClose();
    if (state.id === item.state.id) return;
    dispatch({
      area: 'workItem',
      kind: 'setState',
      itemId: item.id,
      key: item.key,
      revision: item.revision,
      stateId: state.id,
      stateName: state.name,
      fromStateId: item.state.id,
      fromStateName: item.state.name,
    });
  };

  return (
    <BottomSheet title="Change state" open={open} onClose={onClose} testID={changeStateSheetId}>
      {query.isError ? (
        <View testID={stateSheetErrorId}>
          <ErrorState
            title="Couldn't load the states"
            action={{ label: 'Retry', onPress: () => void query.refetch(), busy: query.isFetching }}
          />
        </View>
      ) : query.data === undefined ? (
        <Skeleton rows={4} />
      ) : (
        <ScrollView style={{ maxHeight: 360 }}>
          {states.map((state) => {
            const current = state.id === item.state.id;
            return (
              <Pressable
                key={state.id}
                testID={stateOptionId(state.name)}
                accessibilityRole="button"
                accessibilityState={{ selected: current }}
                onPress={() => pick(state)}
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
                    color: colors.text,
                    fontSize: theme.font.size.md,
                    fontWeight: current ? theme.font.weight.bold : theme.font.weight.regular,
                  }}
                >
                  {state.name}
                </Text>
                {current ? (
                  <Text style={{ color: colors.accent, fontSize: theme.font.size.md }}>✓</Text>
                ) : null}
              </Pressable>
            );
          })}
        </ScrollView>
      )}
    </BottomSheet>
  );
};
