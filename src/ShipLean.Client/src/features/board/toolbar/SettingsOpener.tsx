import { useSearchParams } from 'react-router-dom';
import { boardTestIds as ids } from '../testIds';

/** Opens the board settings dialog on its Columns pane (the page reads ?settings=). */
export function useOpenBoardSettings(): () => void {
  const [params, setParams] = useSearchParams();
  return () => {
    const next = new URLSearchParams(params);
    next.set('settings', 'columns');
    setParams(next);
  };
}

// ⚙ Board settings: opens the settings dialog on its Columns pane (the page reads ?settings=).
export function SettingsOpener() {
  const open = useOpenBoardSettings();
  return (
    <button
      type="button"
      data-testid={ids.settingsOpen}
      className="btn-ghost"
      title="Columns, swimlanes, card fields, styles, tag colours, annotations, card reordering"
      onClick={open}
    >
      ⚙ Board settings
    </button>
  );
}
