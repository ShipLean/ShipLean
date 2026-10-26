import { agentsTestIds } from '../testIds';

export const readScope = 'shiplean:read';

// The three scopes an agent can hold, in the order the consent page lists them. `phrase` completes "wants to: …".
export const agentScopes = [
  {
    scope: readScope,
    label: 'Read',
    phrase: 'read your projects',
    testId: agentsTestIds.consentScopeRead,
  },
  {
    scope: 'shiplean:write',
    label: 'Write',
    phrase: 'capture and update work',
    testId: agentsTestIds.consentScopeWrite,
  },
  {
    scope: 'shiplean:plan',
    label: 'Plan',
    phrase: 'plan sprints',
    testId: agentsTestIds.consentScopePlan,
  },
] as const;

/** What a scope lets the agent do, in the consent page's words; an unknown scope shows as itself. */
export function scopePhrase(scope: string): string {
  return agentScopes.find((known) => known.scope === scope)?.phrase ?? scope;
}
