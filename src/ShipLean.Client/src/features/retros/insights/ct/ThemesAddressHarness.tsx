import { useLocation } from 'react-router-dom';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { RetroActionDialogProvider } from '../../../retroShared/actionDialog/RetroActionDialogProvider';
import { RecurringThemes } from '../RecurringThemes';
import { insightsProject } from './insightsProject';

/** The Recurring themes part with the address it writes shown beside it, since a component test has no browser bar to read. */
export function ThemesAddressHarness({ insights }: { insights: RetroInsightsDto }) {
  const { search } = useLocation();
  return (
    <RetroActionDialogProvider projectId={insightsProject.id}>
      <span data-testid="test-address">{search}</span>
      <RecurringThemes project={insightsProject} insights={insights} />
    </RetroActionDialogProvider>
  );
}
