import { Link } from 'react-router-dom';
import type { InstanceProjectDto } from '../../../api/projects';
import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { routePath } from '../../../routes';
import { settingsTestIds } from '../testIds';

/** One project: its admins, size, and what you can do with it (open its settings, or join as admin). */
export function ProjectRow({
  project,
  joining,
  onJoin,
}: {
  project: InstanceProjectDto;
  joining: boolean;
  onJoin(): void;
}) {
  const isAdmin = project.yourRole === 'admin';
  return (
    <tr
      data-testid={settingsTestIds.projectsRow}
      data-project-key={project.key}
      data-no-active-admin={!project.hasActiveAdmin}
      className="border-b border-edge"
    >
      <td data-testid={settingsTestIds.projectsKey} className="px-3 py-2 font-mono font-semibold">
        {project.key}
      </td>
      <td className="px-3 py-2">{project.name}</td>
      <td data-testid={settingsTestIds.projectsMembers} className="px-3 py-2 text-right">
        {project.memberCount}
      </td>
      <td data-testid={settingsTestIds.projectsAdmins} className="px-3 py-2">
        <span className="flex items-center gap-1.5">
          {project.admins.map((admin) => (
            <Avatar key={admin.id} {...admin} />
          ))}
          {project.hasActiveAdmin ? null : (
            <span data-testid={settingsTestIds.projectsNoAdmin} className="text-caution">
              none active
            </span>
          )}
        </span>
      </td>
      <td data-testid={settingsTestIds.projectsItems} className="px-3 py-2 text-right">
        {project.itemCount}
      </td>
      <td data-testid={settingsTestIds.projectsYou} className="px-3 py-2">
        {project.yourRole === null ? (
          <span className="text-ink-muted">Not a member</span>
        ) : isAdmin ? (
          <Chip tone="accent">Admin</Chip>
        ) : (
          'Member'
        )}
      </td>
      <td className="px-3 py-2 text-right">
        {isAdmin ? (
          <Link
            data-testid={settingsTestIds.projectsSettings}
            className="btn-ghost"
            to={routePath('projectSettings', { key: project.key, tab: 'general' })}
          >
            Settings
          </Link>
        ) : (
          <button
            type="button"
            data-testid={settingsTestIds.projectsJoin}
            className="btn-ghost"
            disabled={joining}
            onClick={onJoin}
          >
            Join as admin
          </button>
        )}
      </td>
    </tr>
  );
}
