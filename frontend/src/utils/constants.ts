export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

if (!API_BASE_URL) {
  throw new Error('VITE_API_BASE_URL environment variable is not defined. Check your .env.local file.');
}

/** Maximum length for a custom alias (FR-LINK-3) */
export const MAX_ALIAS_LENGTH = 50;
/** Minimum length for a custom alias (FR-LINK-3) */
export const MIN_ALIAS_LENGTH = 3;
/** Pattern for valid custom aliases (FR-LINK-3) */
export const ALIAS_PATTERN = /^[a-zA-Z0-9_-]{3,50}$/;
