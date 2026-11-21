import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { fixTagLine, countSections, type AgentId } from './agentSections';
import { putBack } from './placeholders';
import { checkPrompt } from './promptChecks';

const AGENTS: { id: AgentId; label: string; addId: string }[] = [
  { id: 'claude-code', label: 'Claude Code', addId: ids.checkAddClaude },
  { id: 'codex-cli', label: 'Codex CLI', addId: ids.checkAddOpenAiCli },
  { id: 'cursor', label: 'Cursor', addId: ids.checkAddCursor },
];

const SMALL = 'text-[length:var(--text-xs)]';
const HEADING = `m-0 mb-1 font-semibold uppercase tracking-wide text-muted ${SMALL}`;
const LINK_BUTTON = `cursor-pointer border-0 bg-transparent p-0 text-accent ${SMALL}`;

// The editor's side panel: placeholders with Put back, agent-specific sections with Add and Did you mean.
export function EditorChecks(props: {
  text: string;
  prompt: PromptDto;
  onChange: (text: string) => void;
}) {
  const { text, prompt, onChange } = props;
  const { canSave, problems } = checkPrompt(text, prompt);
  const missing = new Set(
    problems.filter((p) => p.kind === 'placeholderMissing').map((p) => p.placeholder),
  );
  const unknown = problems.filter((p) => p.kind === 'placeholderUnknown');
  const unknownTags = problems.filter((p) => p.kind === 'unknownTag');
  const errors = problems.filter(
    (p) => p.kind === 'unclosed' || p.kind === 'nested' || p.kind === 'strayClose',
  );
  const counts = countSections(text);
  return (
    <aside
      data-testid={ids.checks}
      aria-label={`Checks for ${prompt.name}`}
      data-length={text.length}
      data-can-save={canSave}
      className={`grid content-start gap-3 ${SMALL}`}
    >
      <section data-testid={ids.checkPlaceholders}>
        <h4 className={HEADING}>Placeholders</h4>
        {prompt.placeholders.length === 0 ? (
          <span className="text-muted">This prompt has none.</span>
        ) : (
          <div className="grid gap-1">
            {prompt.placeholders.map((placeholder) => {
              const isMissing = missing.has(placeholder);
              return (
                <div
                  key={placeholder}
                  data-testid={ids.checkPlaceholderRow}
                  className="flex items-center justify-between gap-2"
                >
                  <code
                    data-testid={isMissing ? ids.checkPlaceholderMissing : undefined}
                    className={isMissing ? 'text-alert line-through' : undefined}
                  >
                    {placeholder}
                  </code>
                  {isMissing ? (
                    <button
                      type="button"
                      data-testid={ids.checkPutBack}
                      className={LINK_BUTTON}
                      aria-label={`Put back ${placeholder}`}
                      onClick={() => onChange(putBack(text, placeholder, prompt.builtInText))}
                    >
                      Put back
                    </button>
                  ) : (
                    <span data-testid={ids.checkPlaceholderOk} className="text-ok">
                      ✓
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        )}
        {missing.size > 0 ? (
          <p className="m-0 mt-1.5 font-medium text-alert">
            Without {[...missing].join(', ')} agents won't get that part. You can still save.
          </p>
        ) : null}
        {unknown.map((problem) => (
          <p
            key={problem.placeholder}
            data-testid={ids.checkPlaceholderUnknown}
            className="m-0 mt-1.5 font-medium text-alert"
          >
            {problem.message}
          </p>
        ))}
      </section>
      <section data-testid={ids.checkSections}>
        <h4 className={HEADING}>Agent-specific sections</h4>
        <p className="m-0 mb-1 text-muted">
          Put a tag on its own line before and after text that only one agent should get. Other
          agents never see it.
        </p>
        {AGENTS.map((agent) => (
          <div key={agent.id} className="flex items-center justify-between gap-2">
            <code>{`<${agent.id}>`}</code>
            <span>{agent.label}</span>
            <span
              data-testid={ids.checkSectionCount}
              data-agent={agent.id}
              className={counts[agent.id] > 0 ? 'text-ok' : 'text-muted'}
            >
              {counts[agent.id] === 0
                ? 'none'
                : `${counts[agent.id]} section${counts[agent.id] > 1 ? 's' : ''}`}
            </span>
          </div>
        ))}
        {unknownTags.map((problem) => (
          <p
            key={`${problem.line}-unknown`}
            data-testid={ids.checkUnknownTag}
            className="m-0 mt-1.5 font-medium text-alert"
          >
            Line {problem.line}: {problem.message} Did you mean{' '}
            <code>{`<${problem.suggestion}>`}</code>?{' '}
            <button
              type="button"
              data-testid={ids.checkFixTag}
              className={LINK_BUTTON}
              onClick={() => onChange(fixTagLine(text, problem.line!, problem.suggestion!))}
            >
              Fix it
            </button>
          </p>
        ))}
        {errors.map((problem) => (
          <p
            key={`${problem.line}-${problem.kind}`}
            data-testid={ids.checkUnclosedTag}
            data-kind={problem.kind}
            role="alert"
            className="m-0 mt-1.5 font-medium text-danger"
          >
            Line {problem.line}: {problem.message}
          </p>
        ))}
        {errors.length > 0 ? (
          <p data-testid={ids.checkProblem} className="m-0 mt-1.5 font-medium text-danger">
            Save is blocked until every agent section is fixed.
          </p>
        ) : null}
        <div data-testid={ids.checkAddSection} className="mt-1 flex flex-wrap gap-x-3">
          {AGENTS.map((agent) => (
            <button
              key={agent.id}
              type="button"
              data-testid={agent.addId}
              className={LINK_BUTTON}
              onClick={() => onChange(`${text}\n<${agent.id}>\n\n</${agent.id}>\n`)}
            >
              + {agent.label}
            </button>
          ))}
        </div>
      </section>
      <section>
        <h4 className={HEADING}>Tip</h4>
        <p className="m-0 text-muted">
          Add project rules at the end, so built-in updates still apply cleanly.
        </p>
      </section>
    </aside>
  );
}
