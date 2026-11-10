import type { DecisionDto } from '@shiplean/core/api/asks';

// The Markdown posted to Discussion when a person discusses a decision: the decision as one quoted line
// (every line of it, if the question runs over several), a blank line, then the person's text.
export function discussQuote(decision: DecisionDto, text: string): string {
  const quoted = `**${decision.label}** · ${decision.question}: ${decision.currentAnswer}`
    .split(/\r?\n/)
    .map((line) => (line.trim() === '' ? '>' : `> ${line}`))
    .join('\n');
  return `${quoted}\n\n${text.trim()}`;
}
