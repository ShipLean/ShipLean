// Criteria shapes. C#: ShipLean.Contracts.Criteria. The persisted documents are in persisted.ts.
import type { CriteriaJson } from './persisted';

/** clauseIndex is null for an error on the document itself (no clauses). */
export interface CriteriaValidationError {
  clauseIndex: number | null;
  field: string;
  code: string;
  message: string;
}

/** criteria is null when there is none. */
export interface CriteriaDto {
  criteria: CriteriaJson | null;
  errors: CriteriaValidationError[];
}
