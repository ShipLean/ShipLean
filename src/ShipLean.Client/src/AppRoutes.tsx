import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import { AttachmentPage } from './pages/AttachmentPage';
import { MobileSignInPage } from './features/mobileSignIn/MobileSignInPage';
import { AuthorizePage } from './pages/AuthorizePage';
import { LoginPage } from './features/auth/LoginPage';
import { RootRedirect } from './features/auth/RootRedirect';
import { BacklogPage } from './pages/BacklogPage';
import { BoardPage } from './pages/BoardPage';
import { CapacityPage } from './pages/CapacityPage';
import { FeatureBoardPage } from './pages/FeatureBoardPage';
import { ImportPage } from './pages/ImportPage';
import { InstanceSettingsPage } from './pages/InstanceSettingsPage';
import { NewProjectPage } from './pages/NewProjectPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProfilePage } from './pages/ProfilePage';
import { ProjectSettingsPage } from './pages/ProjectSettingsPage';
import { RetroFeaturesPage } from './pages/RetroFeaturesPage';
import { RetroInsightsPage } from './pages/RetroInsightsPage';
import { RetroItemPage } from './pages/RetroItemPage';
import { RetrosPage } from './pages/RetrosPage';
import { SprintBacklogPage } from './pages/SprintBacklogPage';
import { SprintRetroPage } from './pages/SprintRetroPage';
import { SprintDemoPage } from './pages/SprintDemoPage';
import { SprintsPage } from './pages/SprintsPage';
import { TaskboardPage } from './pages/TaskboardPage';
import { WorkItemPage } from './pages/WorkItemPage';
import { routePattern } from './routes';
import { Shell } from './Shell';

// The Changes page loads on its own: the diff renderer comes with it and only there.
const TaskChangesPage = lazy(() =>
  import('./pages/TaskChangesPage').then((m) => ({ default: m.TaskChangesPage })),
);

// The full route table, separate from main.tsx (which owns the router) so component tests can mount the exact routing
// the real app uses. Paths and ids are in routes.ts; docs/navigation.md lists the pages and their slots.
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path={routePattern('mobileSignIn')} element={<MobileSignInPage />} />
      <Route element={<Shell />}>
        <Route path={routePattern('newProject')} element={<NewProjectPage />} />
        <Route path={routePattern('instanceSettings')} element={<InstanceSettingsPage />} />
        <Route path={routePattern('profile')} element={<ProfilePage />} />
        <Route path={routePattern('importStart')} element={<ImportPage />} />
        <Route path={routePattern('authorize')} element={<AuthorizePage />} />
        <Route path={routePattern('storiesBacklog')} element={<BacklogPage level="stories" />} />
        <Route path={routePattern('featuresBacklog')} element={<BacklogPage level="features" />} />
        <Route path={routePattern('epicsBacklog')} element={<BacklogPage level="epics" />} />
        <Route path={routePattern('workItem')} element={<WorkItemPage />} />
        <Route path={routePattern('workItemAttachment')} element={<AttachmentPage />} />
        <Route
          path={routePattern('taskChanges')}
          element={
            <Suspense fallback={null}>
              <TaskChangesPage />
            </Suspense>
          }
        />
        <Route path={routePattern('featureBoard')} element={<FeatureBoardPage />} />
        <Route path={routePattern('storiesBoard')} element={<BoardPage level="stories" />} />
        <Route path={routePattern('featuresBoard')} element={<BoardPage level="features" />} />
        <Route path={routePattern('epicsBoard')} element={<BoardPage level="epics" />} />
        <Route path={routePattern('sprints')} element={<SprintsPage />} />
        <Route path={routePattern('sprintBacklog')} element={<SprintBacklogPage />} />
        <Route path={routePattern('taskboard')} element={<TaskboardPage />} />
        <Route path={routePattern('capacity')} element={<CapacityPage />} />
        <Route path={routePattern('sprintRetro')} element={<SprintRetroPage />} />
        <Route path={routePattern('sprintDemo')} element={<SprintDemoPage />} />
        <Route path={routePattern('retros')} element={<RetrosPage />} />
        <Route path={routePattern('retroFeatures')} element={<RetroFeaturesPage />} />
        <Route path={routePattern('retroItem')} element={<RetroItemPage />} />
        <Route path={routePattern('retroInsights')} element={<RetroInsightsPage />} />
        <Route path={routePattern('jiraImport')} element={<ImportPage />} />
        <Route path={routePattern('projectSettings')} element={<ProjectSettingsPage />} />
        <Route path="/p/:key/*" element={<NotFoundPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="/" element={<RootRedirect />} />
    </Routes>
  );
}
