// The built-in prompts, a project's custom versions of them, and every prompts route. C#: ShipLean.Contracts.Prompts.
// Round-tripped by prompts.test.ts over TestSupport/DtoFixtures/prompts. Labels are made by the server; the client formats nothing.
import { api } from './client';

/** The twelve prompts, in stage order. */
export const PROMPT_KEYS = [
  'issueProposal',
  'work',
  'guide',
  'planReviewer',
  'lead',
  'brief',
  'report',
  'codeReviewer',
  'fix',
  'retro',
  'retroAnalysis',
  'sprintDemo',
] as const;
export type PromptKey = (typeof PROMPT_KEYS)[number];

export const PROMPT_STAGES = [
  'intake',
  'shape',
  'plan',
  'build',
  'review',
  'retro',
  'sprintDemo',
] as const;
export type PromptStage = (typeof PROMPT_STAGES)[number];

export const PROMPT_STATUSES = ['builtIn', 'custom', 'builtInUpdated', 'usingBuiltIn'] as const;
export type PromptStatus = (typeof PROMPT_STATUSES)[number];

export const PROMPT_VERSION_SOURCES = ['editor', 'builtInUpdate', 'migrated'] as const;
export type PromptVersionSource = (typeof PROMPT_VERSION_SOURCES)[number];

export const PROMPT_LAYER_KINDS = [
  'serverInstructions',
  'prompt',
  'agentSections',
  'waysOfWorking',
  'clientNotes',
] as const;
export type PromptLayerKind = (typeof PROMPT_LAYER_KINDS)[number];

/** The version in use, else the newest. */
export interface PromptCustomDto {
  number: number;
  text: string;
  inUse: boolean;
  by: string;
  atLabel: string;
  note: string;
  baseLabel: string;
  /** The built-in this version started from, for the editor's live diff. */
  baseText: string;
}

export interface PromptUpdateDto {
  changedLabel: string;
  baseLabel: string;
  patch: string;
  appliesCleanly: boolean;
  mergedText: string;
}

export interface PromptDto {
  key: PromptKey;
  file: string;
  name: string;
  whenRuns: string;
  servedBy: string;
  readBy: string;
  note: string | null;
  placeholders: string[];
  insertedParts: string[];
  status: PromptStatus;
  builtInText: string;
  builtInHash: string;
  custom: PromptCustomDto | null;
  yourChangesPatch: string | null;
  /** Only when the status is builtInUpdated. */
  update: PromptUpdateDto | null;
  /** AgentSections.Check errors in the text in use; above 0 every agent gets only untagged text. */
  sectionErrors: number;
}

export interface PromptStageDto {
  id: PromptStage;
  name: string;
  when: string;
  prompts: PromptDto[];
}

export interface PromptAgentDto {
  id: string;
  label: string;
}

export interface PromptBuiltInOnlyDto {
  name: string;
  why: string;
}

export interface PromptsPageDto {
  stages: PromptStageDto[];
  customCount: number;
  canEdit: boolean;
  adminNames: string[];
  agents: PromptAgentDto[];
  builtInOnly: PromptBuiltInOnlyDto[];
}

export interface PromptVersionDto {
  number: number;
  note: string;
  by: string;
  atLabel: string;
  inUse: boolean;
  source: PromptVersionSource;
  text: string;
}

/** Newest first. */
export interface PromptHistoryDto {
  key: PromptKey;
  versions: PromptVersionDto[];
  baseLabel: string | null;
}

export interface PromptLayerDto {
  kind: PromptLayerKind;
  title: string;
  text: string | null;
  planned: boolean;
  note: string | null;
}

/** `text` is the prompt exactly as that agent gets it, placeholders unfilled. */
export interface PromptAssembledDto {
  key: PromptKey;
  agent: string;
  layers: PromptLayerDto[];
  text: string;
}

export interface PromptSaveDto {
  text: string;
  note: string;
  source: PromptVersionSource;
}

export interface PromptSavedDto {
  key: PromptKey;
  status: PromptStatus;
  inUseVersion: number | null;
}

const promptsPath = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}/prompts`;
const promptPath = (projectId: string, key: PromptKey) =>
  `${promptsPath(projectId)}/${encodeURIComponent(key)}`;

/** GET api/projects/{id}/prompts: every prompt, grouped by stage. */
export function getPrompts(projectId: string): Promise<PromptsPageDto> {
  return api.get<PromptsPageDto>(promptsPath(projectId));
}

/** GET …/prompts/{prompt}/history: every version, newest first. */
export function getPromptHistory(projectId: string, key: PromptKey): Promise<PromptHistoryDto> {
  return api.get<PromptHistoryDto>(`${promptPath(projectId, key)}/history`);
}

/** GET …/prompts/{prompt}/assembled?agent=: the layers and text that agent gets. */
export function getPromptAssembled(
  projectId: string,
  key: PromptKey,
  agent: string,
): Promise<PromptAssembledDto> {
  return api.get<PromptAssembledDto>(
    `${promptPath(projectId, key)}/assembled?agent=${encodeURIComponent(agent)}`,
  );
}

/** POST …/prompts/{prompt}/versions: saves a new custom version and puts it in use (admin). */
export function savePromptVersion(
  projectId: string,
  key: PromptKey,
  body: PromptSaveDto,
): Promise<PromptSavedDto> {
  return api.post<PromptSavedDto>(`${promptPath(projectId, key)}/versions`, body);
}

/** POST …/prompts/{prompt}/use-built-in: agents get the built-in again; versions are kept (admin). */
export function usePromptBuiltIn(projectId: string, key: PromptKey): Promise<PromptSavedDto> {
  return api.post<PromptSavedDto>(`${promptPath(projectId, key)}/use-built-in`);
}

/** POST …/prompts/{prompt}/versions/{number}/use: puts that version back in use (admin). */
export function usePromptVersion(
  projectId: string,
  key: PromptKey,
  number: number,
): Promise<PromptSavedDto> {
  return api.post<PromptSavedDto>(
    `${promptPath(projectId, key)}/versions/${encodeURIComponent(String(number))}/use`,
  );
}

/** POST …/prompts/{prompt}/update/keep: keeps the custom version as it is until the built-in changes again (admin). */
export function keepPromptMine(projectId: string, key: PromptKey): Promise<PromptSavedDto> {
  return api.post<PromptSavedDto>(`${promptPath(projectId, key)}/update/keep`);
}
