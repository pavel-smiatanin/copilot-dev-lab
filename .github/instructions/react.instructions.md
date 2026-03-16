---
applyTo: "**/*.{ts,tsx}"
---

# React & TypeScript Coding Instructions

## Project Structure

```
src/
  components/     # Reusable UI components (PascalCase filenames)
  pages/          # Route-level page components
  hooks/          # Custom hooks (use* prefix)
  services/       # Axios client instance and API call functions
  types/          # TypeScript interfaces for API requests/responses
  utils/          # Pure helper functions (no side-effects)
  assets/         # Static files (images, icons)
```

- Each folder has a single responsibility — do not mix concerns (e.g., no API calls in `components/`).
- Co-locate component-specific styles, tests, and sub-components in the same directory as the component.

## Naming Conventions

| Symbol | Convention | Example |
|---|---|---|
| Component files | PascalCase | `LinkCard.tsx`, `CreateLinkForm.tsx` |
| Component default export | PascalCase, matches filename | `export default function LinkCard` |
| Props interface | `<ComponentName>Props` | `LinkCardProps`, `CreateLinkFormProps` |
| Custom hooks | `use` prefix, camelCase | `useCreateLink`, `useLinkStats` |
| Hook files | camelCase, matches hook name | `useCreateLink.ts` |
| Service functions | camelCase, verb + noun | `createLink`, `getLinkStats` |
| Service files | camelCase | `linksService.ts`, `apiClient.ts` |
| Type / interface files | camelCase | `linkTypes.ts`, `apiTypes.ts` |
| Utility functions | camelCase, descriptive | `formatExpiry`, `buildShortUrl` |
| Constants | SCREAMING_SNAKE_CASE | `MAX_ALIAS_LENGTH`, `API_BASE_URL` |
| CSS Module class names | camelCase | `styles.cardWrapper`, `styles.errorMessage` |
| Page components | PascalCase with `Page` suffix | `HomePage`, `StatsPage` |
| Test files | `{ComponentOrHook}.test.tsx` | `LinkCard.test.tsx`, `useCreateLink.test.ts` |

## TypeScript

- Enable strict mode in `tsconfig.json`: `"strict": true`.
- Define interfaces (or types) for every API request and response shape in `src/types/`.
- Prefer `interface` for object shapes (extendable); use `type` for unions, intersections, and aliases.
- Avoid `any` — use `unknown` for truly dynamic data and narrow with type guards before use.
- Avoid type assertions (`as SomeType`) unless you have provably narrowed the type. Never use `as any`.
- Explicitly type function return values for all exported functions and hooks. Infer types for local variables where obvious.
- Use `readonly` modifiers on interface properties that should not be mutated after construction.
- Prefer `Record<K, V>` over index signatures `{ [key: string]: V }` where key type is known.

```typescript
// Preferred: explicit API types in src/types/
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
  readonly expiresAt: string | null;
}
```

## Component Guidelines

- Use **functional components with hooks only** — no class components.
- One component per file; the filename must match the default export (PascalCase).
- Keep components small and focused on rendering. Extract logic into custom hooks.
- Declare the props interface directly above the component function.
- Destructure props in the function signature.
- Do not pass anonymous functions directly as prop values — define them with `useCallback` or extract them.
- Avoid inline object/array literals in JSX props — they create a new reference on every render.
- Use **CSS Modules** for all component styles (`styles.module.css`); do not use inline `style` props except for truly dynamic values (e.g., widths derived from state).

```tsx
// Preferred pattern
interface LinkCardProps {
  readonly alias: string;
  readonly shortUrl: string;
  readonly expiresAt: string | null;
  readonly onCopy: (url: string) => void;
}

export default function LinkCard({ alias, shortUrl, expiresAt, onCopy }: LinkCardProps) {
  const handleCopy = useCallback(() => onCopy(shortUrl), [onCopy, shortUrl]);

  return (
    <div className={styles.card}>
      <span className={styles.alias}>{alias}</span>
      <button onClick={handleCopy}>Copy</button>
    </div>
  );
}
```

## Hooks

- Custom hook filenames use the `use` prefix in camelCase: `useCreateLink.ts`.
- Each custom hook encapsulates a single concern (data fetching, form state, clipboard, etc.).
- Do not call hooks conditionally or inside loops — follow the Rules of Hooks strictly.
- Return a stable, typed object from hooks (not a tuple unless the hook is a simple pair like `useState`).
- Memoize expensive computed values with `useMemo`; memoize callbacks passed to children with `useCallback`.
- Avoid over-memoization — only apply `useMemo`/`useCallback` where there is a measurable re-render cost.

## State Management

- Use **React Query (TanStack Query)** for all server state: creation, fetching, cache invalidation.
- Use `useState` / `useReducer` for local UI state only (form input, modal open/close, validation messages).
- Do not introduce a global state manager (Redux, Zustand, etc.) — the app is too simple to warrant it.
- Keep query keys descriptive and co-located with the hook that uses them:
  ```typescript
  const QUERY_KEYS = {
    linkStats: (alias: string) => ['links', alias, 'stats'] as const,
  } as const;
  ```

## API Integration

- All API calls are encapsulated in `src/services/` — components and hooks must **never** call Axios directly.
- Create a shared Axios instance in `src/services/apiClient.ts` configured with `baseURL` from `import.meta.env.VITE_API_BASE_URL`.
- Service functions return typed promises; never return raw Axios responses — extract `.data`.
- Handle HTTP error responses explicitly:
  - `400` — display validation messages from `error.response.data`
  - `404` — show "not found" / "expired" state
  - `409` — show "alias already taken" message
  - `429` — show "rate limited" message; read `Retry-After` header
  - `500` — show generic error; log to console in development
- Every async operation must surface loading and error states in the UI — never silently swallow errors.

```typescript
// src/services/linksService.ts
import apiClient from './apiClient';
import type { CreateLinkRequest, CreateLinkResponse } from '../types/linkTypes';

export async function createLink(request: CreateLinkRequest): Promise<CreateLinkResponse> {
  const { data } = await apiClient.post<CreateLinkResponse>('/api/v1/links', request);
  return data;
}
```

## React Query Patterns

- Wrap the app in `<QueryClientProvider>` at the root.
- Use `useQuery` for GET operations; use `useMutation` for POST/PUT/DELETE.
- Always provide `onError` handlers in mutations to surface errors to the UI.
- Invalidate related queries after successful mutations instead of manually updating the cache:
  ```typescript
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['links'] });
  }
  ```
- Set sensible `staleTime` values — avoid over-fetching for data that rarely changes.

## Testing

- Use **Vitest** as the test runner — not Jest. Vite's ecosystem is incompatible with Jest transforms.
- Use **React Testing Library** (`@testing-library/react`) for component tests.
- Use **`@testing-library/user-event`** for simulating user interactions (click, type, etc.) over `fireEvent`.
- Use `@testing-library/jest-dom` for DOM assertion matchers (`toBeInTheDocument`, `toBeVisible`, etc.).
- Test **rendered output and user interactions**, not implementation details (no testing of internal state or refs).
- Do not test React Query internals — mock service functions and assert on rendered UI.
- Mock Axios / service modules with `vi.mock(...)` at the top of test files.

```tsx
// Example: testing a component that calls a service
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import * as linksService from '../../services/linksService';
import CreateLinkForm from './CreateLinkForm';

vi.mock('../../services/linksService');

describe('CreateLinkForm', () => {
  it('submits the form and displays the short URL on success', async () => {
    // Arrange
    vi.mocked(linksService.createLink).mockResolvedValue({
      id: '1', alias: 'abc123', shortUrl: 'http://short.ly/abc123', expiresAt: null,
    });
    render(<CreateLinkForm />);

    // Act
    await userEvent.type(screen.getByLabelText(/destination url/i), 'https://example.com');
    await userEvent.click(screen.getByRole('button', { name: /shorten/i }));

    // Assert
    expect(await screen.findByText('http://short.ly/abc123')).toBeInTheDocument();
  });
});
```

## Error Handling UI Patterns

- Every page-level data fetch must have three states: loading skeleton/spinner, error message, and success content.
- Inline validation errors must be associated with their input via `aria-describedby`.
- Do not use `alert()` or `console.error()` as a substitute for proper UI error states.
- Use React Query `isLoading`, `isError`, and `error` states — do not manage these manually.

## Accessibility

- Every interactive element (`button`, `input`, `select`) must have an accessible label (`aria-label`, `aria-labelledby`, or a visible `<label>` with `htmlFor`).
- Use semantic HTML elements (`<button>`, `<nav>`, `<main>`, `<section>`, `<header>`) rather than `<div>` with click handlers.
- Ensure keyboard navigability — do not remove `:focus` outline styles without providing a visible alternative.
- Use `@testing-library/jest-dom` accessibility assertions in tests.

## Performance

- Wrap expensive child components in `React.memo` only when profiling confirms unnecessary re-renders.
- Use `useCallback` for callbacks passed to memoized children.
- Use `useMemo` for expensive computations derived from state/props — not for simple values.
- Code-split route-level pages with `React.lazy` + `Suspense` if the app grows beyond a single page.
- Avoid placing large objects or arrays in state that trigger deep re-renders unnecessarily.

## Code Style

- Use named exports for all non-page components (facilitates tree-shaking); use default exports for page components and components that follow the file-name convention.
- Prefer optional chaining (`?.`) and nullish coalescing (`??`) over verbose null checks.
- Use `const` for all declarations that are not reassigned; `let` only when reassignment is unavoidable.
- Always define the `key` prop when rendering lists; use a stable, unique identifier — never the array index.
- Avoid nested ternary operators in JSX — extract to a named variable or sub-component instead.
- Keep JSX return values clean: extract complex conditional blocks into helper components or functions.

```tsx
// Avoid
{isLoading ? <Spinner /> : isError ? <ErrorMessage /> : <LinkCard />}

// Prefer
function renderContent() {
  if (isLoading) return <Spinner />;
  if (isError) return <ErrorMessage error={error} />;
  return <LinkCard />;
}
return <div>{renderContent()}</div>;
```

## Environment Variables

- Access environment variables only via `import.meta.env.VITE_*` — never `process.env`.
- Validate required variables at app startup; throw a clear error if a required variable is missing.
- Never commit secret values to version control; use `.env.local` for local overrides (already in `.gitignore`).
- Define variables in `.env.example` to document what is required.

## Security

- Never render user-supplied HTML directly — avoid `dangerouslySetInnerHTML` unless explicitly sanitizing with a trusted library.
- Do not store sensitive data (passwords, tokens) in `localStorage` or `sessionStorage`.
- Validate and encode URL parameters before constructing links or redirect targets.
- Treat all data returned from the API as untrusted until rendered through React's default XSS-safe rendering.
