import { useSearchParams } from 'react-router-dom';
import { boardTestIds as ids } from '../testIds';

// ⚙ Board settings: opens the settings dialog on its Columns pane (the page reads ?settings=).
export function SettingsOpener() {
  const [params, setParams] = useSearchParams();
  return (
    <button
      type="button"
      data-testid={ids.settingsOpen}
      className="btn-ghost"
      title="Columns, swimlanes, card fields, styles, tag colours, annotations, card reordering"
      onClick={() => {
        const next = new URLSearchParams(params);
        next.set('settings', 'columns');
        setParams(next);
      }}
    >
      ⚙ Board settings
    </button>
  );
}
