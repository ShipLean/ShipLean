import { useState, type KeyboardEvent } from 'react';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '../../../api/refs';
import { isStoryLevel, typeName } from '../../../api/workItemTypes';
import type { ParentRefDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { FieldMenu } from '../../workItem/form/FieldMenu';
import { ParentCandidates } from '../../workItem/form/ParentCandidates';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { backlogTestIds as ids } from '../testIds';
import { usePasteIntake } from './usePasteIntake';
import type { AddFields } from './useAddChain';

const types = WORK_ITEM_TYPE_IDS.filter(isStoryLevel);

// The type, title, parent and points of an add row, with Add and Esc. The caller owns the type and parent (they stick
// between rows) and gets the fields on Enter. Title and points are kept here and clear after a row is made.
export function AddRowFields({
  projectId,
  type,
  parent,
  onType,
  onParent,
  hint,
  error,
  onAdd,
  onClose,
}: {
  projectId: string;
  type: WorkItemTypeId;
  parent: ParentRefDto | null;
  onType: (type: WorkItemTypeId) => void;
  onParent: (parent: ParentRefDto | null) => void;
  hint: string;
  error: string | null;
  // Resolves true when the row was made: the title and points clear. A failed create resolves false and keeps them.
  onAdd: (fields: AddFields) => Promise<boolean>;
  onClose: () => void;
}) {
  const [title, setTitle] = useState('');
  const [points, setPoints] = useState('');
  const [pointsError, setPointsError] = useState(false);
  const paste = usePasteIntake(type);

  async function add() {
    const text = title.trim();
    if (text === '') return;
    const raw = points.trim();
    const value = raw === '' ? null : Number(raw);
    if (value !== null && (!Number.isFinite(value) || value < 0)) {
      setPointsError(true);
      return;
    }
    setPointsError(false);
    if (await onAdd({ type, title: text, points: value, parent })) {
      setTitle('');
      setPoints('');
    }
  }

  function onKeyDown(event: KeyboardEvent) {
    event.stopPropagation();
    if (event.key === 'Enter') {
      event.preventDefault();
      void add();
    } else if (event.key === 'Escape') onClose();
  }

  return (
    <div className="flex items-center gap-[var(--space-2)]">
      <select
        data-testid={ids.addType}
        aria-label="Type"
        className="field"
        value={type}
        onChange={(event) => onType(event.target.value as WorkItemTypeId)}
        onKeyDown={onKeyDown}
      >
        {types.map((id) => (
          <option key={id} value={id}>
            {typeName(id)}
          </option>
        ))}
      </select>
      <input
        autoFocus
        data-testid={ids.addTitle}
        aria-label="Title"
        className="field flex-1"
        placeholder="Title, then Enter. Or paste many lines"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={onKeyDown}
        {...paste}
      />
      <FieldMenu
        buttonTestId={ids.addParent}
        buttonLabel="Parent"
        button={
          parent ? (
            <>
              <TypeIcon type={parent.type.id} />
              <span className="max-w-40 truncate">{parent.title}</span>
            </>
          ) : (
            <span className="text-ink-muted">No parent</span>
          )
        }
      >
        {(close) => (
          <ParentCandidates
            projectId={projectId}
            itemId=""
            types={parentTypesOf(type)}
            value={parent}
            onPick={(picked) => {
              onParent(picked);
              close();
            }}
          />
        )}
      </FieldMenu>
      <input
        data-testid={ids.addPoints}
        aria-label="Points"
        aria-invalid={pointsError}
        inputMode="decimal"
        placeholder="Pts"
        className="field w-16 text-right"
        value={points}
        onChange={(event) => setPoints(event.target.value)}
        onKeyDown={onKeyDown}
      />
      <button
        type="button"
        data-testid={ids.addSubmit}
        className="btn-signal"
        onClick={() => void add()}
      >
        Add
      </button>
      <button type="button" data-testid={ids.addClose} className="btn-ghost" onClick={onClose}>
        Esc
      </button>
      <span className="text-[length:var(--text-xs)] text-ink-muted">{hint}</span>
      {error ? (
        <span
          data-testid={ids.addError}
          role="alert"
          className="text-[length:var(--text-sm)] text-alert"
        >
          {error}
        </span>
      ) : null}
    </div>
  );
}
