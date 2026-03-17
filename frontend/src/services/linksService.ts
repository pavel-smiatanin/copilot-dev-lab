import apiClient from './apiClient';
import type {
  CreateLinkRequest,
  CreateLinkResponse,
  LinkStatsResponse,
  UnlockRequest,
  UnlockResponse,
} from '../types/linkTypes';

export async function createLink(request: CreateLinkRequest): Promise<CreateLinkResponse> {
  const { data } = await apiClient.post<CreateLinkResponse>('/api/v1/links', request);
  return data;
}

export async function getLinkStats(id: string): Promise<LinkStatsResponse> {
  const { data } = await apiClient.get<LinkStatsResponse>(`/api/v1/links/${id}/stats`);
  return data;
}

export async function unlockLink(alias: string, request: UnlockRequest): Promise<UnlockResponse> {
  const { data } = await apiClient.post<UnlockResponse>(`/api/v1/links/${alias}/unlock`, request);
  return data;
}
