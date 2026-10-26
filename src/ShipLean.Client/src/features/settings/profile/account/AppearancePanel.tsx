import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { DENSITIES, THEMES, type Density, type MeDto, type Theme } from '../../../../api/me';
import { fetchMe, meKey } from '../../../../queries/shared';
import { applyAppearance } from '../../../../theme';
import { savePreference } from '../../settingsApi';
import { settingsTestIds } from '../../testIds';
import { errorText } from './errorText';
import { FormRow } from '../identity/FormRow';
import { PanelFrame } from './PanelFrame';
import { SegmentedChoice } from './SegmentedChoice';

const THEME_LABELS: Record<Theme, string> = { os: 'Match my OS', light: 'Light', dark: 'Dark' };
const DENSITY_LABELS: Record<Density, string> = { compact: 'Compact', comfortable: 'Comfortable' };

const APPEARANCE_KEY = ['appearance'] as const;

interface Choice {
  key: 'theme' | 'density';
  value: string;
}

/** Theme and density: shown at once, stored on the account, and put back if the server refuses. */
export function AppearancePanel() {
  const queryClient = useQueryClient();
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const [refusal, setRefusal] = useState<string | null>(null);
  // Saves run one at a time, so the server ends on the last pick. A refusal puts back only its own setting,
  // and only while the cache still shows what that pick set; the settled save then reads the server's truth.
  const save = useMutation({
    mutationKey: APPEARANCE_KEY,
    scope: { id: 'appearance' },
    mutationFn: ({ key, value }: Choice) => savePreference(key, value),
    onMutate: async ({ key, value }) => {
      setRefusal(null);
      await queryClient.cancelQueries({ queryKey: meKey });
      const previous = queryClient.getQueryData<MeDto>(meKey);
      if (previous !== undefined) {
        const next = { ...previous, [key]: value } as MeDto;
        queryClient.setQueryData(meKey, next);
        applyAppearance(next.theme, next.density);
      }
      return { before: previous?.[key] };
    },
    onError: (error, { key, value }, context) => {
      setRefusal(errorText(error, 'That choice could not be saved.'));
      const current = queryClient.getQueryData<MeDto>(meKey);
      if (current === undefined || context?.before === undefined || current[key] !== value) return;
      const restored = { ...current, [key]: context.before } as MeDto;
      queryClient.setQueryData(meKey, restored);
      applyAppearance(restored.theme, restored.density);
    },
    onSettled: () => {
      if (queryClient.isMutating({ mutationKey: APPEARANCE_KEY }) <= 1) {
        void queryClient.invalidateQueries({ queryKey: meKey });
      }
    },
  });
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
            onPick={(value) => save.mutate({ key: 'theme', value })}
          />
        </FormRow>
        <FormRow label="Density">
          <SegmentedChoice
            label="Density"
            testId={settingsTestIds.densityOption}
            options={DENSITIES.map((value) => ({ value, label: DENSITY_LABELS[value] }))}
            selected={me.density}
            onPick={(value) => save.mutate({ key: 'density', value })}
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
