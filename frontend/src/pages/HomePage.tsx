import { useCallback, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import type { AxiosError } from 'axios';
import { createLink } from '../services/linksService';
import type { ConflictAlternative, CreateLinkRequest, CreateLinkResponse } from '../types/linkTypes';
import styles from './HomePage.module.css';

interface FormState {
  destinationUrl: string;
  customAlias: string;
  expiresAt: string;
  password: string;
}

const INITIAL_FORM: FormState = {
  destinationUrl: '',
  customAlias: '',
  expiresAt: '',
  password: '',
};

export default function HomePage() {
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [result, setResult] = useState<CreateLinkResponse | null>(null);
  const [copied, setCopied] = useState(false);
  const [conflictAlternatives, setConflictAlternatives] = useState<readonly ConflictAlternative[]>([]);

  const mutation = useMutation<CreateLinkResponse, AxiosError, CreateLinkRequest>({
    mutationFn: createLink,
    onSuccess: (data) => {
      setResult(data);
      setConflictAlternatives([]);
    },
    onError: (error) => {
      if (error.response?.status === 409) {
        const body = error.response.data as { alternatives?: ConflictAlternative[] };
        setConflictAlternatives(body.alternatives ?? []);
      }
    },
  });

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  }, []);

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      setResult(null);
      setConflictAlternatives([]);
      setCopied(false);

      const request: CreateLinkRequest = {
        destinationUrl: form.destinationUrl,
        ...(form.customAlias ? { customAlias: form.customAlias } : {}),
        ...(form.expiresAt ? { expiresAt: new Date(form.expiresAt).toISOString() } : {}),
        ...(form.password ? { password: form.password } : {}),
      };

      mutation.mutate(request);
    },
    [form, mutation],
  );

  const handleCopy = useCallback(async () => {
    if (result) {
      await navigator.clipboard.writeText(result.shortUrl);
      setCopied(true);
    }
  }, [result]);

  const handleAlternativeSelect = useCallback((alias: string) => {
    setForm((prev) => ({ ...prev, customAlias: alias }));
    setConflictAlternatives([]);
  }, []);

  function renderContent() {
    if (result) {
      return (
        <div className={styles.result} role="status" aria-live="polite">
          <p className={styles.resultLabel}>Your short link is ready:</p>
          <div className={styles.shortUrlRow}>
            <a href={result.shortUrl} className={styles.shortUrl} target="_blank" rel="noopener noreferrer">
              {result.shortUrl}
            </a>
            <button className={styles.copyButton} onClick={handleCopy} aria-label="Copy short URL to clipboard">
              {copied ? 'Copied!' : 'Copy'}
            </button>
          </div>
          {result.expiresAt && (
            <p className={styles.expiry}>Expires: {new Date(result.expiresAt).toLocaleString()}</p>
          )}
          <button
            className={styles.createAnotherButton}
            onClick={() => {
              setResult(null);
              setForm(INITIAL_FORM);
              setCopied(false);
            }}
          >
            Create another link
          </button>
        </div>
      );
    }

    return null;
  }

  const is409 = mutation.isError && mutation.error?.response?.status === 409;
  const isGenericError = mutation.isError && !is409;

  return (
    <main className={styles.page}>
      <h1 className={styles.heading}>URL Shortener</h1>
      <p className={styles.subheading}>Paste a long URL and get a short link instantly.</p>

      {!result && (
        <form className={styles.form} onSubmit={handleSubmit} noValidate>
          <div className={styles.field}>
            <label htmlFor="destinationUrl" className={styles.label}>
              Destination URL <span aria-hidden="true">*</span>
            </label>
            <input
              id="destinationUrl"
              name="destinationUrl"
              type="url"
              className={styles.input}
              value={form.destinationUrl}
              onChange={handleChange}
              placeholder="https://example.com/very/long/url"
              required
              aria-required="true"
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="customAlias" className={styles.label}>
              Custom alias <span className={styles.optional}>(optional)</span>
            </label>
            <input
              id="customAlias"
              name="customAlias"
              type="text"
              className={styles.input}
              value={form.customAlias}
              onChange={handleChange}
              placeholder="my-link"
              minLength={3}
              maxLength={50}
              pattern="[a-zA-Z0-9_\-]{3,50}"
              aria-describedby={is409 ? 'alias-error' : undefined}
            />
            {is409 && (
              <p id="alias-error" className={styles.fieldError} role="alert">
                This alias is already taken.
              </p>
            )}
            {conflictAlternatives.length > 0 && (
              <div className={styles.alternatives}>
                <p className={styles.alternativesLabel}>Suggestions:</p>
                <ul className={styles.alternativesList}>
                  {conflictAlternatives.map((alt) => (
                    <li key={alt.alias}>
                      <button
                        type="button"
                        className={styles.alternativeButton}
                        onClick={() => handleAlternativeSelect(alt.alias)}
                      >
                        {alt.alias}
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          <div className={styles.field}>
            <label htmlFor="expiresAt" className={styles.label}>
              Expiry date <span className={styles.optional}>(optional)</span>
            </label>
            <input
              id="expiresAt"
              name="expiresAt"
              type="datetime-local"
              className={styles.input}
              value={form.expiresAt}
              onChange={handleChange}
            />
          </div>

          <div className={styles.field}>
            <label htmlFor="password" className={styles.label}>
              Password <span className={styles.optional}>(optional)</span>
            </label>
            <input
              id="password"
              name="password"
              type="password"
              className={styles.input}
              value={form.password}
              onChange={handleChange}
              placeholder="Protect with a password"
              autoComplete="new-password"
            />
          </div>

          {isGenericError && (
            <p className={styles.genericError} role="alert">
              {mutation.error?.response?.status === 429
                ? 'Too many requests. Please wait before trying again.'
                : 'Something went wrong. Please try again.'}
            </p>
          )}

          <button type="submit" className={styles.submitButton} disabled={mutation.isPending}>
            {mutation.isPending ? 'Shortening…' : 'Shorten URL'}
          </button>
        </form>
      )}

      {renderContent()}
    </main>
  );
}
