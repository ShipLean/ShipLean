import { Route, Routes } from 'react-router-dom';
import { AttachmentPage } from './pages/AttachmentPage';
import { AuthorizePage } from './pages/AuthorizePage';
import { LoginPage } from './features/auth/LoginPage';
import { RootRedirect } from './features/auth/RootRedirect';
import { BacklogPage } from './pages/BacklogPage';
import { BoardPage } from './pages/BoardPage';
import { CapacityPage } from './pages/CapacityPage';
import { ImportPage } from './pages/ImportPage';
import { InstanceSettingsPage } from './pages/InstanceSettingsPage';
import { NewProjectPage } from './pages/NewProjectPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProfilePage } from './pages/ProfilePage';
import { ProjectSettingsPage } from './pages/ProjectSettingsPage';
import { SprintBacklogPage } from './pages/SprintBacklogPage';
import { SprintsPage } from './pages/SprintsPage';
import { TaskboardPage } from './pages/TaskboardPage';
import { WorkItemPage } from './pages/WorkItemPage';
import { routePattern } from './routes';
import { Shell } from './Shell';

// The full route table, separate from main.tsx (which owns the router) so component tests can mount the exact routing
// the real app uses. Paths and ids are in routes.ts; docs/navigation.md lists the pages and their slots.
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
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
        <Route path={routePattern('storiesBoard')} element={<BoardPage level="stories" />} />
        <Route path={routePattern('featuresBoard')} element={<BoardPage level="features" />} />
        <Route path={routePattern('epicsBoard')} element={<BoardPage level="epics" />} />
        <Route path={routePattern('sprints')} element={<SprintsPage />} />
        <Route path={routePattern('sprintBacklog')} element={<SprintBacklogPage />} />
        <Route path={routePattern('taskboard')} element={<TaskboardPage />} />
        <Route path={routePattern('capacity')} element={<CapacityPage />} />
        <Route path={routePattern('jiraImport')} element={<ImportPage />} />
        <Route path={routePattern('projectSettings')} element={<ProjectSettingsPage />} />
        <Route path="/p/:key/*" element={<NotFoundPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="/" element={<RootRedirect />} />
    </Routes>
  );
}
