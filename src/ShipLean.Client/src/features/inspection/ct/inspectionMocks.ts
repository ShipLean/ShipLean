// CT mock data for every inspection route. The JSON is IMPORTED from ShipLean.TestSupport/Inspection/Shapes (the shapes
// the C# tests pin), never copied, so a mock cannot drift from the contract.
import type { Page } from 'playwright';
import type {
  AgentRunsSectionDto,
  AgentSetupDto,
  InspectionHealthDto,
  InspectionProjectSettingsDto,
  RunPageDto,
  RunRowDetailDto,
} from '@shiplean/core/api/inspection';
import sectionJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/agent-runs-section.json' with { type: 'json' };
import setupJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/agent-setup.json' with { type: 'json' };
import healthJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/inspection-health.json' with { type: 'json' };
import settingsJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/inspection-project-settings.json' with { type: 'json' };
import runPageJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/run-page.json' with { type: 'json' };
import rowDetailJson from '../../../../../ShipLean.TestSupport/Inspection/Shapes/run-row-detail.json' with { type: 'json' };

export const agentRunsSection = sectionJson as unknown as AgentRunsSectionDto;
export const agentSetup = setupJson as unknown as AgentSetupDto;
export const inspectionHealth = healthJson as unknown as InspectionHealthDto;
export const inspectionSettings = settingsJson as unknown as InspectionProjectSettingsDto;
/** The synthetic traced Claude Code run (`sess-cc-1`). */
export const claudeRunPage = runPageJson as unknown as RunPageDto;
export const runRowDetail = rowDetailJson as unknown as RunRowDetailDto;

// The pinned shapes hold one run page, the traced Claude Code one. The log-only runs (Cursor `sess-cu-1`, which is in
// the pinned section, and the Codex CLI run) get a page derived from it with the log-only fields changed, until
// T0a pins a run page for each.
// TODO: replace with pinned log-only run pages when the shapes directory has them.
export const logOnlyRunPage: RunPageDto = {
  ...claudeRunPage,
  logOnly: true,
  banner: 'This agent sends events only: no model-call timing.',
  grafanaUrl: null,
};

/** Every request the inspection routes saw, in order, so a spec can say "no inspection request". */
export interface InspectionRequests {
  urls: string[];
}

/** Answers every inspection route with the pinned shapes (or the overrides) and records the requests. */
export async function stubInspection(
  page: Page,
  overrides: { section?: AgentRunsSectionDto; runPage?: RunPageDto } = {},
): Promise<InspectionRequests> {
  const seen: InspectionRequests = { urls: [] };
  await page.route('**/api/inspection/**', (route) => {
    const url = new URL(route.request().url());
    seen.urls.push(url.pathname + url.search);
    const path = url.pathname;
    if (path.endsWith('/settings')) return route.fulfill({ json: inspectionSettings });
    if (path.endsWith('/agent-setup')) return route.fulfill({ json: agentSetup });
    if (path.endsWith('/health')) return route.fulfill({ json: inspectionHealth });
    if (/\/runs\/[^/]+\/rows\/[^/]+$/.test(path)) return route.fulfill({ json: runRowDetail });
    if (/\/runs\/[^/]+$/.test(path))
      return route.fulfill({ json: overrides.runPage ?? claudeRunPage });
    return route.fulfill({ json: overrides.section ?? agentRunsSection });
  });
  await page.route('**/api/projects/*/usage/inspection', (route) => {
    seen.urls.push(new URL(route.request().url()).pathname);
    return route.fulfill({ status: 204 });
  });
  return seen;
}
