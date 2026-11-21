import page from '../../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompts-page.json' with { type: 'json' };
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { PromptsPageDto } from '@shiplean/core/api/prompts';

/** The pinned page (admin view): issueProposal using built-in, guide built-in updated, brief custom v2 in use. */
export const adminPage = page as PromptsPageDto;

/** The same page as a member gets it: nothing can be changed. */
export const memberPage: PromptsPageDto = { ...adminPage, canEdit: false };

export const adminProject = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
export const memberProject = { id: 'p-ship', key: 'SHIP', role: 'member' } as ProjectDto;

export const PROMPTS_URL = '**/api/projects/*/prompts';
export const PROMPTS_OPENED_URL = '**/api/projects/*/usage/prompts-opened';
