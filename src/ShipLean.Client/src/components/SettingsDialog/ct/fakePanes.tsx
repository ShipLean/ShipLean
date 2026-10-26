import type { PaneDef, PaneProps } from '../types';
import { settingsHarnessIds as ids } from './harnessIds';

export interface FakeDoc {
  a: string;
  b: string;
  c: string;
}

// A pane that edits one string of the document through one input.
function fakePane(key: keyof FakeDoc, label: string): PaneDef<FakeDoc> {
  function Pane({ draft, onChange }: PaneProps<FakeDoc>) {
    return (
      <div data-testid={`${ids.pane}-${key}`}>
        <input
          data-testid={`${ids.input}-${key}`}
          value={draft[key]}
          onChange={(event) => onChange({ ...draft, [key]: event.target.value })}
        />
      </div>
    );
  }
  // Pane B warns while its value is empty: a warning computed from the draft.
  const warning =
    key === 'b' ? (draft: FakeDoc) => (draft.b === '' ? 'Pane B is empty' : null) : undefined;
  return { id: key, label, sections: [key], component: Pane, warning };
}

export const fakeGroups = [
  { label: 'First', panes: [fakePane('a', 'Pane A'), fakePane('b', 'Pane B')] },
  { label: 'Second', panes: [fakePane('c', 'Pane C')] },
];
