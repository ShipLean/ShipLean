import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { fixTagLine, countSections, type AgentId } from './agentSections';
import { putBack } from './placeholders';
import { checkPrompt, type PromptProblem } from './promptChecks';
import { SideHeading } from './SideHeading';

const AGENTS: { id: AgentId; label: string; addId: string }[] = [
  { id: 'claude-code', label: 'Claude Code', addId: ids.checkAddClaude },
  { id: 'codex-cli', label: 'Codex CLI', addId: ids.checkAddOpenAiCli },
  { id: 'cursor', label: 'Cursor', addId: ids.checkAddCursor },
];

const LINK_BUTTON = 'cursor-pointer border-0 bg-transparent p-0 text-signal';
const CHIP =
  'inline-flex h-5 items-center rounded-[var(--radius-xs)] px-1.5 font-mono text-[length:var(--text-2xs)]';
// Warnings still let you save, so they are amber; errors block Save, so they are red (design-gaps §13).
const WARNING = 'm-0 mt-1.5 font-medium text-caution';
const ERROR = 'm-0 mt-1.5 font-medium text-alert';

interface UnknownTag {
  tag: string;
  suggestion: string;
  lines: number[];
}

// One entry per unknown tag name, however many lines (its open and close tags) use it.
function unknownTags(problems: PromptProblem[]): UnknownTag[] {
  const byTag = new Map<string, UnknownTag>();
  for (const problem of problems) {
    if (problem.kind !== 'unknownTag') continue;
    const tag = problem.tag!;
    const entry = byTag.get(tag);
    if (entry === undefined) {
      byTag.set(tag, { tag, suggestion: problem.suggestion!, lines: [problem.line!] });
    } else {
      entry.lines.push(problem.line!);
    }
  }
  return [...byTag.values()];
}

// The editor's side panel: placeholders with Put back, agent-specific sections with Add and Did you mean.
export function EditorChecks(props: {
  text: string;
  prompt: PromptDto;
  onChange: (text: string) => void;
}) {
  const { text, prompt, onChange } = props;
  const { canSave, problems } = checkPrompt(text, prompt);
  const missing = problems
    .filter((p) => p.kind === 'placeholderMissing')
    .map((p) => p.placeholder!);
  const unknown = problems.filter((p) => p.kind === 'placeholderUnknown');
  const tags = unknownTags(problems);
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
      className="grid content-start gap-3"
    >
      <section data-testid={ids.checkPlaceholders}>
        <SideHeading>Placeholders</SideHeading>
        {prompt.placeholders.length === 0 ? (
          <span className="text-ink-muted">This prompt has none.</span>
        ) : (
          <div className="grid gap-1">
            {prompt.placeholders.map((placeholder) => {
              const isMissing = missing.includes(placeholder);
              return (
                <div
                  key={placeholder}
                  data-testid={ids.checkPlaceholderRow}
                  className="flex items-center justify-between gap-1.5"
                >
                  <code
                    data-testid={isMissing ? ids.checkPlaceholderMissing : undefined}
                    className={`${CHIP} ${isMissing ? 'bg-caution/20 line-through' : 'bg-sunken'}`}
                  >
                    {placeholder}
                  </code>
                  {isMissing ? (
                    <button
                      type="button"
                      data-testid={ids.checkPutBack}
                      className={`${LINK_BUTTON} text-[length:var(--text-2xs)]`}
                      aria-label={`Put back ${placeholder}`}
                      onClick={() => onChange(putBack(text, placeholder, prompt.builtInText))}
                    >
                      Put back
                    </button>
                  ) : (
                    <span data-testid={ids.checkPlaceholderOk} className="text-good">
                      ✓
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        )}
        {missing.length > 0 ? (
          <p data-testid={ids.checkPlaceholderWarning} className={WARNING}>
            Without{' '}
            {missing.map((placeholder, i) => (
              <span key={placeholder}>
                {i > 0 ? ', ' : null}
                <code>{placeholder}</code>
              </span>
            ))}{' '}
            agents won’t get that part. You can still save.
          </p>
        ) : null}
        {unknown.map((problem) => (
          <p
            key={problem.placeholder}
            data-testid={ids.checkPlaceholderUnknown}
            className={WARNING}
          >
            <code>{problem.placeholder}</code> isn’t one ShipLean fills in for this prompt; it stays
            as typed.
          </p>
        ))}
      </section>
      <section data-testid={ids.checkSections}>
        <SideHeading>Agent-specific sections</SideHeading>
        <p className="m-0 mb-1.5 text-ink-muted">
          Put a tag on its own line before and after text that only one agent should get. Other
          agents never see it.
        </p>
        <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-1.5 gap-y-0.5">
          {AGENTS.map((agent) => (
            <div key={agent.id} className="contents">
              <code className="font-mono text-[length:var(--text-2xs)] text-notice">{`<${agent.id}>`}</code>
              <span>{agent.label}</span>
              <span
                data-testid={ids.checkSectionCount}
                data-agent={agent.id}
                className={`text-right whitespace-nowrap ${counts[agent.id] > 0 ? 'text-good' : 'text-ink-muted'}`}
              >
                {counts[agent.id] === 0
                  ? 'none'
                  : `${counts[agent.id]} section${counts[agent.id] > 1 ? 's' : ''}`}
              </span>
            </div>
          ))}
        </div>
        {tags.map((unknownTag) => (
          <p
            key={unknownTag.tag}
            data-testid={ids.checkUnknownTag}
            data-tag={unknownTag.tag}
            className={WARNING}
          >
            <code>{`<${unknownTag.tag}>`}</code> isn’t an agent ShipLean recognises, so every agent
            gets it as typed. Did you mean <code>{`<${unknownTag.suggestion}>`}</code>?{' '}
            <button
              type="button"
              data-testid={ids.checkFixTag}
              className={LINK_BUTTON}
              onClick={() =>
                onChange(
                  unknownTag.lines.reduce(
                    (fixed, line) => fixTagLine(fixed, line, unknownTag.suggestion),
                    text,
                  ),
                )
              }
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
            className={ERROR}
          >
            Line {problem.line}: {problem.message}
          </p>
        ))}
        {errors.length > 0 ? (
          <p data-testid={ids.checkProblem} className={ERROR}>
            Save is blocked until every agent section is fixed.
          </p>
        ) : null}
        <div data-testid={ids.checkAddSection} className="mt-1 flex flex-wrap gap-x-2">
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
    </aside>
  );
}
