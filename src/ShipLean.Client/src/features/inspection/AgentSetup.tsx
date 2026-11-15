import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useAgentSetup } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';

export const SETUP_AGENTS = [
  { id: 'claude-code', label: 'Claude Code' },
  { id: 'codex-cli', label: 'Codex CLI' },
  { id: 'cursor', label: 'Cursor' },
] as const;
export type SetupAgent = (typeof SETUP_AGENTS)[number]['id'];

/** The agent a `?agent=` value names, or undefined when it names none. */
export const asSetupAgent = (value: string | null): SetupAgent | undefined =>
  SETUP_AGENTS.find((a) => a.id === value)?.id;

export interface AgentSetupProps {
  project: ProjectDto;
  /** The agent tab to open on; Claude Code when left out. */
  initialAgent?: SetupAgent;
}

// The Agent setup sub-tab: the files to give each agent, with copy buttons. Read-only; it works while Inspection is off for the project.
export function AgentSetup({ project, initialAgent = 'claude-code' }: AgentSetupProps) {
  const [agent, setAgent] = useState<SetupAgent>(initialAgent);
  const [draft, setDraft] = useState('');
  const [item, setItem] = useState('');
  const setup = useAgentSetup(project.key, agent, item);
  return (
    <div data-testid={ids.inspectionAgentSetup} data-project={project.key} className="grid gap-4">
      <div role="tablist" className="flex gap-1">
        {SETUP_AGENTS.map((a) => (
          <button
            key={a.id}
            type="button"
            role="tab"
            data-testid={ids.inspectionAgentPick}
            data-agent={a.id}
            aria-selected={agent === a.id}
            onClick={() => setAgent(a.id)}
            className={`h-[30px] rounded-control px-3 text-[length:var(--text-sm)] ${
              agent === a.id ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted'
            }`}
          >
            {a.label}
          </button>
        ))}
      </div>
      <label className="grid max-w-[360px] gap-1 text-[length:var(--text-sm)] text-ink-muted">
        Item to tag runs with (optional)
        <input
          data-testid={ids.inspectionItemInput}
          value={draft}
          placeholder={`${project.key}-123`}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={() => setItem(draft.trim())}
          onKeyDown={(e) => {
            if (e.key === 'Enter') setItem(draft.trim());
          }}
          className="h-[30px] rounded-control border border-edge bg-panel px-2 text-ink"
        />
      </label>
      {setup.isPending ? (
        <p className="text-ink-muted">Loading</p>
      ) : setup.isError ? (
        <p role="alert" className="text-alert">
          Could not load the setup{item === '' ? '' : ` for ${item}`}.
        </p>
      ) : (
        <>
          {setup.data.files.map((file) => (
            <SetupFile key={file.name} name={file.name} content={file.content} />
          ))}
          <div
            data-testid={ids.inspectionSetupNotes}
            className="grid gap-1 text-[length:var(--text-sm)] text-ink-muted"
          >
            {setup.data.notes.map((note) => (
              <p key={note} className="m-0">
                {note}
              </p>
            ))}
            <a
              href={setup.data.docsUrl}
              target="_blank"
              rel="noreferrer"
              className="text-ink underline"
            >
              {setup.data.agentLabel} setup guide
            </a>
          </div>
        </>
      )}
    </div>
  );
}

function SetupFile({ name, content }: { name: string; content: string }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(content);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };
  return (
    <section data-testid={ids.inspectionSetupFile} data-name={name} className="grid gap-1">
      <div className="flex items-center justify-between">
        <code className="text-[length:var(--text-sm)] text-ink">{name}</code>
        <button
          type="button"
          data-testid={ids.inspectionSetupCopy}
          onClick={() => void copy()}
          className="h-[26px] rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre className="m-0 max-h-[320px] overflow-auto rounded-control border border-edge bg-panel p-3 text-[length:var(--text-sm)] text-ink">
        {content}
      </pre>
    </section>
  );
}
