import { listProjects, projectsKey } from '@shiplean/core/api/projects';
import { RETRO_CATEGORIES, type RetroCategory } from '@shiplean/core/api/retros';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { View } from 'react-native';
import { useActionRunner, usePending } from '../../actions/ActionRunnerProvider';
import { UNDO } from '../../actions/undo';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { Chip } from '../../ui/Chip';
import { TextField } from '../../ui/TextField';
import { CATEGORY_LABELS } from '../notes/notesModel';
import { useRetroActions } from '../RetroActions';
import { retroAddCategoryId, retroIds } from '../testIds';
import { aboutOf } from './about';

export interface AddPointSheetProps {
  projectId: string;
  sprintId: string | null;
  open: boolean;
  onClose: () => void;
  // Preselected when the Notes view has one open; a typed point from nowhere has none.
  category: RetroCategory | null;
}

// The project's key, from the projects list Projects already loaded (the same query useProject reads).
function useProjectKey(projectId: string): string | undefined {
  const projects = useQuery({ queryKey: projectsKey, queryFn: listProjects });
  return projects.data?.find((p) => p.id === projectId)?.key;
}

// Add a point: a category and the text. The point is about the item the text names (the first PROJECTKEY-n), else the
// shown sprint (P-R1). It is sent at once and can be undone from the toast; the sheet stays open until the send
// lands, and shows what the server said when it refuses. The form is mounted only while the sheet is open, so each
// opening starts clean.
export function AddPointSheet(props: AddPointSheetProps) {
  const { open, onClose } = props;
  return (
    <BottomSheet title="Add a point" open={open} onClose={onClose} testID={retroIds.addSheet}>
      {open ? <AddPointForm {...props} /> : null}
    </BottomSheet>
  );
}

function AddPointForm({ projectId, sprintId, onClose, category }: AddPointSheetProps) {
  const theme = useTheme();
  const runner = useActionRunner();
  const { dispatch } = useRetroActions();
  const projectKey = useProjectKey(projectId);
  const [picked, setPicked] = useState<RetroCategory | null>(category);
  const [text, setText] = useState('');
  const [batchId, setBatchId] = useState<string | null>(null);
  const mine = usePending((p) => batchId !== null && p.batchId === batchId);

  // Sent, or queued offline: the toast carries it from here, so the sheet closes.
  useEffect(() => {
    if (batchId === null) return undefined;
    const check = () => {
      const batch = runner.pending().filter((p) => p.batchId === batchId);
      if (batch.length > 0 && batch.every((p) => p.state === 'sent' || p.state === 'queued'))
        onClose();
    };
    check();
    return runner.subscribe(check);
  }, [runner, batchId, onClose]);

  const failed = mine.find((p) => p.state === 'failed');
  const sending = mine.some((p) => p.state === 'sending');
  const body = text.trim();
  const about = aboutOf(body, projectKey, sprintId);
  const canAdd =
    projectKey !== undefined &&
    picked !== null &&
    body !== '' &&
    (about.key !== null || about.sprintId !== null) &&
    !sending;

  const add = () => {
    if (picked === null || !canAdd) return;
    setBatchId(
      dispatch(
        {
          area: 'retro',
          kind: 'addPoint',
          itemId: null,
          key: about.key,
          projectId,
          sprintId: about.sprintId,
          category: picked,
          categoryGuessed: false,
          text: body,
        },
        { origin: 'typed', undo: UNDO.typed },
      ),
    );
  };

  return (
    <View style={{ gap: theme.space[3], paddingTop: theme.space[2] }}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
        {RETRO_CATEGORIES.map((c) => (
          <Chip
            key={c}
            testID={retroAddCategoryId(c)}
            label={CATEGORY_LABELS[c]}
            selected={picked === c}
            onPress={() => setPicked(c)}
          />
        ))}
      </View>
      <TextField
        testID={retroIds.addText}
        label="Point"
        value={text}
        onChangeText={setText}
        hint={
          about.key === null
            ? 'About this sprint. Name an item by its key to make it about that.'
            : `About ${about.key}.`
        }
        error={failed?.error ?? undefined}
      />
      <Button
        testID={retroIds.addSubmit}
        label="Add"
        disabled={!canAdd}
        busy={sending}
        onPress={add}
      />
    </View>
  );
}
