import { useNavigate } from 'react-router-dom';
import { showToast } from '../../../components/toastStore';
import { useAppearanceSave } from '../../settings/profile/account/useAppearanceSave';
import { useMe } from '../../shell/useShellQueries';
import { useShellProject } from '../../shell/useShellProject';
import type { QuickSearchResults } from '../types';
import { buildActions, isDarkTheme } from './actions/actions';

/** The Actions group, narrowed by the query. */
export function useActionSource(query: string): QuickSearchResults {
  const navigate = useNavigate();
  const projectKey = useShellProject().key;
  const theme = useMe().data?.theme ?? 'os';
  const { save } = useAppearanceSave({
    onRefused: () => showToast('That choice could not be saved.', { tone: 'danger' }),
  });
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const rows = buildActions(
    {
      projectKey,
      theme,
      prefersDark,
      navigate,
      toggleTheme: () => save('theme', isDarkTheme(theme, prefersDark) ? 'light' : 'dark'),
    },
    query,
  );
  return {
    groups: rows.length === 0 ? [] : [{ id: 'actions', title: 'Actions', rows }],
    status: 'ready',
    note: null,
    retry: null,
  };
}
