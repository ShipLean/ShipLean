import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroFeatures } from '../useRetroQueries';
import { useTouchShell } from '../../../touch/useTouchShell';
import { FeaturesTable } from './FeaturesTable';
import { FeaturesToolbar } from './FeaturesToolbar';
import {
  parseRetroFeaturesAddress,
  withRetroFeaturesValue,
  type RetroFeaturesShow,
} from './retroFeaturesAddress';
import { rowsToShow } from './rowsToShow';
import { PhoneFeatureList } from './touch/PhoneFeatureList';
import { PhoneFeaturesToolbar } from './touch/PhoneFeaturesToolbar';

/**
 * Retrospectives › Features: one row per Feature counting its own retro and its stories' and bugs', then the stories
 * and bugs with no Feature. The filter and the search work on the rows already loaded and live in the address
 * (retroFeaturesAddress.ts).
 */
export function RetroFeaturesPanel({ project }: { project: ProjectDto }) {
  const [params, setParams] = useSearchParams();
  const address = parseRetroFeaturesAddress(params);
  const onShow = (show: RetroFeaturesShow) =>
    setParams(withRetroFeaturesValue(params, { show }), { replace: true });
  const onSearch = (q: string) =>
    setParams(withRetroFeaturesValue(params, { q }), { replace: true });
  const query = useRetroFeatures(project.id);
  const phone = useTouchShell().viewport === 'phone';
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <RetroQueryState query={query}>
        {(data) => {
          const features = rowsToShow(data.features, address);
          const loose = address.show === 'waiting' ? [] : rowsToShow(data.loose, address);
          return (
            <div
              data-testid={ids.retroFeaturesPanel}
              data-project={project.key}
              className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-3"
            >
              {phone ? (
                <>
                  <PhoneFeaturesToolbar address={address} onShow={onShow} onSearch={onSearch} />
                  <PhoneFeatureList
                    projectKey={project.key}
                    address={address}
                    features={features}
                    loose={loose}
                    onClearSearch={() => onSearch('')}
                  />
                </>
              ) : (
                <>
                  <FeaturesToolbar
                    address={address}
                    featureCount={features.length}
                    onShow={onShow}
                    onSearch={onSearch}
                  />
                  <FeaturesTable projectKey={project.key} features={features} loose={loose} />
                </>
              )}
              <p className="text-[length:var(--text-xs)] text-ink-muted">
                Counts cover the Feature's own retro and its stories' and bugs' retros. Went well
                needs no decision.
              </p>
            </div>
          );
        }}
      </RetroQueryState>
    </RetroActionDialogProvider>
  );
}
