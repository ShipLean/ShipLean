import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { useFeaturesActions } from '../FeaturesActions';
import { useFeatureStatesQuery } from '../queries';
import {
  featureStateApplyId,
  featureStateOptionId,
  featureStateSheetId,
  featureStateWarningId,
} from '../testIds';
import { closingWarning, openCount } from './openChildren';

export interface ChangeStateSheetProps {
  row: FeatureRowDto;
  projectId: string;
  open: boolean;
  onClose: () => void;
}

// Every state of a Feature, by order (P-W2: the server has no transition rules). Closing a Feature that still has open
// children puts the warning on the line and still applies (D8).
export function ChangeStateSheet({ row, projectId, open, onClose }: ChangeStateSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useFeaturesActions();
  const query = useFeatureStatesQuery(projectId);
  const [pickedId, setPickedId] = useState<string | null>(null);
  const states = (query.data ?? [])
    .filter((state) => state.type === 'feature')
    .sort((a, b) => a.order - b.order);
  const picked = states.find((state) => state.id === pickedId) ?? null;
  const warning = picked === null ? null : closingWarning(row.rollups, picked.category);
  const close = () => {
    setPickedId(null);
    onClose();
  };
  const apply = () => {
    if (picked === null) return;
    dispatch({
      area: 'features',
      kind: 'setState',
      itemId: row.id,
      key: row.key,
      revision: row.revision,
      stateId: picked.id,
      stateName: picked.name,
      fromStateId: row.state.id,
      fromStateName: row.state.name,
      closing: picked.category === 'completed',
      openStories: openCount(row.rollups),
    });
    close();
  };
  return (
    <BottomSheet
      title={`${row.key} state`}
      open={open}
      onClose={close}
      testID={featureStateSheetId(row.key)}
    >
      <ScrollView style={{ maxHeight: 360 }}>
        {states.map((state) => {
          const current = state.id === row.state.id;
          const selected = state.id === (pickedId ?? row.state.id);
          return (
            <Pressable
              key={state.id}
              testID={featureStateOptionId(state.name)}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => setPickedId(state.id)}
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
                  fontWeight: selected ? theme.font.weight.bold : theme.font.weight.regular,
                }}
              >
                {state.name}
                {current ? ' (now)' : ''}
              </Text>
              {selected ? (
                <Text style={{ color: colors.accent, fontSize: theme.font.size.md }}>✓</Text>
              ) : null}
            </Pressable>
          );
        })}
      </ScrollView>
      {warning === null ? null : (
        <Text
          testID={featureStateWarningId(row.key)}
          style={{
            color: colors.warn,
            fontSize: theme.font.size.sm,
            fontWeight: theme.font.weight.semibold,
            marginTop: theme.space[3],
          }}
        >
          {warning}
        </Text>
      )}
      <View style={{ marginTop: theme.space[3] }}>
        <Button
          label="Apply"
          testID={featureStateApplyId(row.key)}
          disabled={picked === null || picked.id === row.state.id}
          onPress={apply}
        />
      </View>
    </BottomSheet>
  );
}
