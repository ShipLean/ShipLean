import { useQuery } from '@tanstack/react-query';
import { DENSITIES, THEMES, type Density, type Theme } from '../../../../api/me';
import { fetchMe, meKey } from '../../../../queries/shared';
import { settingsTestIds } from '../../testIds';
import { FormRow } from '../identity/FormRow';
import { PanelFrame } from './PanelFrame';
import { SegmentedChoice } from './SegmentedChoice';
import { useAppearanceSave } from './useAppearanceSave';

const THEME_LABELS: Record<Theme, string> = { os: 'Match my OS', light: 'Light', dark: 'Dark' };
const DENSITY_LABELS: Record<Density, string> = { compact: 'Compact', comfortable: 'Comfortable' };

/** Theme and density: shown at once, stored on the account, and put back if the server refuses. */
export function AppearancePanel() {
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const { save, refusal } = useAppearanceSave();
  if (me === undefined) return null;
  return (
    <PanelFrame
      title="Appearance"
      meta="Applies straight away, on every browser you use"
      testId={settingsTestIds.appearancePanel}
    >
      <div className="grid grid-cols-[130px_minmax(0,1fr)] max-sm:grid-cols-1">
        <FormRow label="Theme">
          <SegmentedChoice
            label="Theme"
            testId={settingsTestIds.themeOption}
            options={THEMES.map((value) => ({ value, label: THEME_LABELS[value] }))}
            selected={me.theme}
            onPick={(value) => save('theme', value)}
          />
        </FormRow>
        <FormRow label="Density">
          <SegmentedChoice
            label="Density"
            testId={settingsTestIds.densityOption}
            options={DENSITIES.map((value) => ({ value, label: DENSITY_LABELS[value] }))}
            selected={me.density}
            onPick={(value) => save('density', value)}
          />
          <span className="text-[length:var(--text-xs)] text-ink-muted">
            Compact fits more rows on backlogs and boards. Comfortable gives rows, controls and the
            side menu more room.
          </span>
          {refusal !== null ? (
            <span
              data-testid={settingsTestIds.appearanceError}
              role="alert"
              className="text-[length:var(--text-xs)] text-alert"
            >
              {refusal}
            </span>
          ) : null}
        </FormRow>
      </div>
    </PanelFrame>
  );
}
