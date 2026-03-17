import { useCallback, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import type { AxiosError } from 'axios';
import { unlockLink } from '../services/linksService';
import type { UnlockRequest, UnlockResponse } from '../types/linkTypes';
import styles from './UnlockPage.module.css';

export default function UnlockPage() {
  const { alias } = useParams<{ alias: string }>();
  const navigate = useNavigate();
  const [password, setPassword] = useState('');

  const mutation = useMutation<UnlockResponse, AxiosError, UnlockRequest>({
    mutationFn: (req) => unlockLink(alias!, req),
    onSuccess: (data) => {
      // Redirect with unlock token so the backend completes the redirect (FR-PASS-3)
      navigate(`/${alias}?token=${encodeURIComponent(data.token)}`);
    },
  });

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      mutation.mutate({ password });
    },
    [mutation, password],
  );

  const is429 = mutation.isError && mutation.error?.response?.status === 429;
  const is401 = mutation.isError && mutation.error?.response?.status === 401;

  function renderError() {
    if (!mutation.isError) return null;
    if (is429) {
      const retryAfter = (mutation.error.response?.headers as Record<string, string>)?.['retry-after'];
      return (
        <p className={styles.error} role="alert">
          Too many failed attempts. Please wait{retryAfter ? ` ${retryAfter} seconds` : ''} before trying again.
        </p>
      );
    }
    if (is401) {
      return (
        <p className={styles.error} role="alert">
          Incorrect password. Please try again.
        </p>
      );
    }
    return (
      <p className={styles.error} role="alert">
        Something went wrong. Please try again.
      </p>
    );
  }

  return (
    <main className={styles.page}>
      <h1 className={styles.heading}>Protected Link</h1>
      <p className={styles.description}>This link is password-protected. Enter the password to continue.</p>

      <form className={styles.form} onSubmit={handleSubmit} noValidate>
        <div className={styles.field}>
          <label htmlFor="password" className={styles.label}>
            Password
          </label>
          <input
            id="password"
            type="password"
            className={styles.input}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            aria-required="true"
            aria-describedby={mutation.isError ? 'unlock-error' : undefined}
            autoFocus
            autoComplete="current-password"
          />
        </div>

        <div id="unlock-error">{renderError()}</div>

        <button type="submit" className={styles.submitButton} disabled={mutation.isPending || is429}>
          {mutation.isPending ? 'Unlocking…' : 'Unlock'}
        </button>
      </form>
    </main>
  );
}
