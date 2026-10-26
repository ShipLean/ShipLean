import type { InstanceProjectDto } from '../../../api/projects';
import { settingsTestIds } from '../testIds';
import { ProjectRow } from './ProjectRow';

export function ProjectsTable({
  projects,
  joining,
  onJoin,
}: {
  projects: InstanceProjectDto[];
  joining: ReadonlySet<string>;
  onJoin(project: InstanceProjectDto): void;
}) {
  return (
    <table
      data-testid={settingsTestIds.projectsTable}
      className="w-full text-left text-[length:var(--text-sm)]"
    >
      <thead className="border-b border-edge text-ink-muted">
        <tr>
          <th className="px-3 py-2 font-medium">Key</th>
          <th className="px-3 py-2 font-medium">Name</th>
          <th className="px-3 py-2 text-right font-medium">Members</th>
          <th className="px-3 py-2 font-medium">Admins</th>
          <th className="px-3 py-2 text-right font-medium">Work items</th>
          <th className="px-3 py-2 font-medium">You</th>
          <th className="px-3 py-2" />
        </tr>
      </thead>
      <tbody>
        {projects.map((project) => (
          <ProjectRow
            key={project.id}
            project={project}
            joining={joining.has(project.id)}
            onJoin={() => onJoin(project)}
          />
        ))}
      </tbody>
    </table>
  );
}
