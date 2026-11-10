import { View } from 'react-native';
import type { BacklogLevel } from '../actions';
import { addRowId } from '../testIds';

export interface AddRowProps {
  projectId: string;
  level: BacklogLevel;
  parent: { id: string; key: string } | null;
}

// TODO: the add row from the design (a text box that adds an item at the top).
export function AddRow({ parent }: AddRowProps) {
  return (
    <View
      testID={addRowId}
      accessibilityLabel={parent === null ? 'Add' : `Add under ${parent.key}`}
    />
  );
}
