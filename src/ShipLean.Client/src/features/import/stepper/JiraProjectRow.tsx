import type { JiraProjectDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';

export function JiraProjectRow({
  project,
  selected,
  onPick,
}: {
  project: JiraProjectDto;
  selected: boolean;
  onPick: () => void;
}) {
  return (
    <tr
      data-testid={ids.projectRow}
      data-key={project.key}
      aria-selected={selected}
      className={`cursor-pointer border-t border-edge ${selected ? 'bg-signal-soft' : ''}`}
      onClick={onPick}
    >
      <td className="px-3 py-2 font-mono">
        <input
          data-testid={ids.projectRadio}
          type="radio"
          name="jira-project"
          className="mr-2"
          aria-label={project.name}
          checked={selected}
          onChange={onPick}
          // The row's click already picks; a radio's own click must not pick twice.
          onClick={(event) => event.stopPropagation()}
        />
        {project.key}
      </td>
      <td className="px-3 py-2">{project.name}</td>
      <td className="px-3 py-2 text-right">{project.issueCount}</td>
      <td className="px-3 py-2">
        {project.importedAs ? (
          <span data-testid={ids.projectImportedAs}>
            Imported as {project.importedAs.projectKey} on {project.importedAs.finishedLabel}
          </span>
        ) : null}
      </td>
    </tr>
  );
}
