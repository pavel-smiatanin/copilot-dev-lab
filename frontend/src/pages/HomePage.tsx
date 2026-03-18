import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import type { AxiosError } from 'axios';
import { createLink } from '../services/linksService';
import type { CreateLinkRequest, CreateLinkResponse, ValidationErrorResponse } from '../types/linkTypes';
import { ALIAS_PATTERN, MIN_ALIAS_LENGTH } from '../utils/constants';
import styles from './HomePage.module.css';

interface FormState {
  destinationUrl: string;
  customAlias: string;
  expiresAt: string;
  password: string;
}

interface FieldErrors {
  destinationUrl?: string;
  customAlias?: string;
}

const INITIAL_FORM: FormState = {
  destinationUrl: '',
  customAlias: '',
  expiresAt: '',
  password: '',
};

function validateForm(form: FormState): FieldErrors {
  const errors: FieldErrors = {};

  if (!form.destinationUrl.trim()) {
    errors.destinationUrl = 'Destination URL is required.';
  } else {
    try {
      const url = new URL(form.destinationUrl.trim());
      if (url.protocol !== 'http:' && url.protocol !== 'https:') {
        errors.destinationUrl = 'URL must use http or https scheme.';
      }
    } catch {
      errors.destinationUrl = 'Please enter a valid URL (e.g. https://example.com).';
    }
  }

  if (form.customAlias && !ALIAS_PATTERN.test(form.customAlias)) {
    errors.customAlias = `Alias must be ${MIN_ALIAS_LENGTH}–50 characters and contain only letters, digits, hyphens, or underscores.`;
  }

  return errors;
}

export default function HomePage() {
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [result, setResult] = useState<CreateLinkResponse | null>(null);
  const [copied, setCopied] = useState(false);
  const [conflictSuggestions, setConflictSuggestions] = useState<readonly string[]>([]);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const mutation = useMutation<CreateLinkResponse, AxiosError, CreateLinkRequest>({
    mutationFn: createLink,
    onSuccess: (data) => {
      setResult(data);
      setConflictSuggestions([]);
      setFieldErrors({});
    },
    onError: (error) => {
      if (error.response?.status === 409) {
        const body = error.response.data as { suggestions?: string[] };
        setConflictSuggestions(body.suggestions ?? []);
      } else if (error.response?.status === 400) {
        const body = error.response.data as ValidationErrorResponse | undefined;
        if (body?.errors) {
          const errors: FieldErrors = {};
          for (const e of body.errors) {
            const key = (e.field.charAt(0).toLowerCase() + e.field.slice(1)) as keyof FieldErrors;
            if (key === 'destinationUrl' || key === 'customAlias') {
              errors[key] = e.message;
            }
          }
          setFieldErrors(errors);
        }
      }
    },
  });

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    setFieldErrors((prev) => ({ ...prev, [name]: undefined }));
    if (name === 'customAlias') {
      setConflictSuggestions([]);
    }
  }, []);

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      setResult(null);
      setConflictSuggestions([]);
      setCopied(false);

      const errors = validateForm(form);
      if (Object.keys(errors).length > 0) {
        setFieldErrors(errors);
        return;
      }
      setFieldErrors({});

      const request: CreateLinkRequest = {
        destinationUrl: form.destinationUrl.trim(),
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
    setConflictSuggestions([]);
    setFieldErrors((prev) => ({ ...prev, customAlias: undefined }));
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
          <div className={styles.resultActions}>
            <Link to={`/stats/${result.id}`} className={styles.statsLink}>
              View stats
            </Link>
            <button
              className={styles.createAnotherButton}
              onClick={() => {
                setResult(null);
                setForm(INITIAL_FORM);
                setCopied(false);
                setFieldErrors({});
              }}
            >
              Create another link
            </button>
          </div>
        </div>
      );
    }

    return null;
  }

  const is409 = mutation.isError && mutation.error?.response?.status === 409;
  const is429 = mutation.isError && mutation.error?.response?.status === 429;
  const is500 = mutation.isError && (mutation.error?.response?.status ?? 500) >= 500;

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
              className={`${styles.input}${fieldErrors.destinationUrl ? ` ${styles.inputError}` : ''}`}
              value={form.destinationUrl}
              onChange={handleChange}
              placeholder="https://example.com/very/long/url"
              aria-required="true"
              aria-describedby={fieldErrors.destinationUrl ? 'destinationUrl-error' : undefined}
            />
            {fieldErrors.destinationUrl && (
              <p id="destinationUrl-error" className={styles.fieldError} role="alert">
                {fieldErrors.destinationUrl}
              </p>
            )}
          </div>

          <div className={styles.field}>
            <label htmlFor="customAlias" className={styles.label}>
              Custom alias <span className={styles.optional}>(optional)</span>
            </label>
            <input
              id="customAlias"
              name="customAlias"
              type="text"
              className={`${styles.input}${fieldErrors.customAlias || is409 ? ` ${styles.inputError}` : ''}`}
              value={form.customAlias}
              onChange={handleChange}
              placeholder="my-link"
              minLength={3}
              maxLength={50}
              aria-describedby={
                fieldErrors.customAlias
                  ? 'customAlias-error'
                  : is409
                    ? 'alias-conflict-error'
                    : undefined
              }
            />
            {fieldErrors.customAlias && (
              <p id="customAlias-error" className={styles.fieldError} role="alert">
                {fieldErrors.customAlias}
              </p>
            )}
            {is409 && !fieldErrors.customAlias && (
              <p id="alias-conflict-error" className={styles.fieldError} role="alert">
                This alias is already taken.
              </p>
            )}
            {conflictSuggestions.length > 0 && (
              <div className={styles.alternatives}>
                <p className={styles.alternativesLabel}>Suggestions:</p>
                <ul className={styles.alternativesList}>
                  {conflictSuggestions.map((alias) => (
                    <li key={alias}>
                      <button
                        type="button"
                        className={styles.alternativeButton}
                        onClick={() => handleAlternativeSelect(alias)}
                      >
                        {alias}
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

          {is429 && (
            <p className={styles.genericError} role="alert">
              Too many requests. Please wait before trying again.
            </p>
          )}
          {is500 && (
            <p className={styles.genericError} role="alert">
              Something went wrong. Please try again.
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
