import type { AsksRegionProps } from './regions';
import { QuestionRow } from './QuestionRow';

// Open questions first, then by number (UI3).
export function QuestionList({ item, asks }: AsksRegionProps) {
  if (asks.questions.length === 0) return null;
  const questions = [...asks.questions].sort((a, b) => {
    const byOpen = Number(b.state === 'open') - Number(a.state === 'open');
    return byOpen !== 0 ? byOpen : a.number - b.number;
  });
  return (
    <div className="grid gap-1">
      <div className="flex min-h-[18px] items-center gap-1.5">
        <span className="text-[length:var(--text-2xs)] font-semibold tracking-[.05em] text-ink-muted uppercase">
          Questions
        </span>
        <span className="text-[length:var(--text-xs)] text-ink-muted tabular-nums">
          {questions.length}
        </span>
        <span className="text-[length:var(--text-xs)] text-ink-muted max-[900px]:hidden">
          · asked by agents with <code>ask</code>. Only a Reply answers one.
        </span>
      </div>
      <ul className="m-0 grid list-none overflow-hidden rounded-panel border border-edge bg-panel p-0">
        {questions.map((question) => (
          <QuestionRow key={question.id} item={item} question={question} />
        ))}
      </ul>
    </div>
  );
}
