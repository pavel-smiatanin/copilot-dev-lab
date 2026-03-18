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
  readonly destinationUrl: string;
  readonly hasPassword: boolean;
  readonly expiresAt: string | null;
  readonly createdAt: string;
}

// Backend 409 response: { message, conflictingAlias, suggestions: string[] }
export interface CreateLinkConflictResponse {
  readonly message: string;
  readonly conflictingAlias: string;
  readonly suggestions: readonly string[];
}

// Backend 400 response: { errors: [{ field, message }] }
export interface ValidationError {
  readonly field: string;
  readonly message: string;
}

export interface ValidationErrorResponse {
  readonly errors: readonly ValidationError[];
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
