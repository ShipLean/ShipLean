// The anonymous server check (GET /api/health). C#: ShipLean.WebApi.Infrastructure.HealthReport.
import { api, type RequestOptions } from './client';

export interface HealthDto {
  status: string;
  version: string;
  search: unknown;
  /** Only when base_url is blank and an agent request arrived on a cluster service name; otherwise left out. */
  publicOrigin?: string | null;
}

export const getHealth = (options?: RequestOptions) => api.get<HealthDto>('/api/health', options);
