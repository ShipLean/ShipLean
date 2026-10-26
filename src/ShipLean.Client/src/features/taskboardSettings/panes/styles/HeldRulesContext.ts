import { createContext, useContext } from 'react';

/** Where a rule row says it is holding criteria that are not valid yet, so the dialog can warn before it closes on them. */
export interface HeldRules {
  report(ruleId: string, held: boolean): void;
}

export const HeldRulesContext = createContext<HeldRules>({ report: () => undefined });

export const useHeldRules = () => useContext(HeldRulesContext);
