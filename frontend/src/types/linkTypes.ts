// Request / response shapes for the links API

export interface CreateLinkRequest {
  readonly destinationUrl: string;
  readonly customAlias?: string;
  readonly expiresAt?: string;
  readonly password?: string;
}

export interface CreateLinkResponse {
  readonly id: string;
  readonly alias: string;
  readonly shortUrl: string;
  readonly title: string | null;
  readonly ogTitle: string | null;
  readonly ogImageUrl: string | null;
  readonly faviconUrl: string | null;
  readonly expiresAt: string | null;
  readonly createdAt: string;
}

export interface ConflictAlternative {
  readonly alias: string;
  readonly shortUrl: string;
}

export interface CreateLinkConflictResponse {
  readonly conflictingAlias: string;
  readonly alternatives: readonly ConflictAlternative[];
}

export interface UnlockRequest {
  readonly password: string;
}

export interface UnlockResponse {
  readonly token: string;
}

export interface DailyVisit {
  readonly date: string;
  readonly count: number;
}

export interface TopReferrer {
  readonly host: string;
  readonly count: number;
}

export interface LinkStatsResponse {
  readonly id: string;
  readonly alias: string;
  readonly shortUrl: string;
  readonly totalVisits: number;
  readonly uniqueVisitors: number;
  readonly visitsByDay: readonly DailyVisit[];
  readonly topReferrers: readonly TopReferrer[];
}
