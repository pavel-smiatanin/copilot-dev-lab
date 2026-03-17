import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getLinkStats } from '../services/linksService';
import type { LinkStatsResponse } from '../types/linkTypes';
import styles from './StatsPage.module.css';

const QUERY_KEYS = {
  linkStats: (id: string) => ['links', id, 'stats'] as const,
} as const;

export default function StatsPage() {
  const { id } = useParams<{ id: string }>();

  const { data, isLoading, isError, error } = useQuery<LinkStatsResponse, Error>({
    queryKey: QUERY_KEYS.linkStats(id!),
    queryFn: () => getLinkStats(id!),
    staleTime: 60_000,
    enabled: !!id,
  });

  function renderContent() {
    if (isLoading) {
      return <p className={styles.status}>Loading statistics…</p>;
    }

    if (isError) {
      const status = (error as { response?: { status?: number } }).response?.status;
      if (status === 404) {
        return <p className={styles.status}>Link not found or has expired.</p>;
      }
      return <p className={styles.status}>Failed to load statistics. Please try again.</p>;
    }

    if (!data) return null;

    return (
      <div className={styles.content}>
        <section className={styles.summary} aria-label="Summary">
          <dl className={styles.statGrid}>
            <div className={styles.statItem}>
              <dt className={styles.statLabel}>Total visits</dt>
              <dd className={styles.statValue}>{data.totalVisits.toLocaleString()}</dd>
            </div>
            <div className={styles.statItem}>
              <dt className={styles.statLabel}>Unique visitors</dt>
              <dd className={styles.statValue}>{data.uniqueVisitors.toLocaleString()}</dd>
            </div>
          </dl>
        </section>

        <section className={styles.section} aria-labelledby="visits-heading">
          <h2 id="visits-heading" className={styles.sectionHeading}>
            Visits (last 30 days)
          </h2>
          {data.visitsByDay.length === 0 ? (
            <p className={styles.empty}>No visits recorded yet.</p>
          ) : (
            <ul className={styles.dayList}>
              {data.visitsByDay.map((day) => (
                <li key={day.date} className={styles.dayRow}>
                  <span className={styles.dayDate}>{new Date(day.date).toLocaleDateString()}</span>
                  <span className={styles.dayBar}>
                    <span
                      className={styles.dayBarFill}
                      style={{
                        width: `${Math.min(100, (day.count / Math.max(...data.visitsByDay.map((d) => d.count), 1)) * 100)}%`,
                      }}
                      aria-label={`${day.count} visits`}
                    />
                  </span>
                  <span className={styles.dayCount}>{day.count}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        {data.topReferrers.length > 0 && (
          <section className={styles.section} aria-labelledby="referrers-heading">
            <h2 id="referrers-heading" className={styles.sectionHeading}>
              Top referrers
            </h2>
            <ol className={styles.referrerList}>
              {data.topReferrers.map((ref) => (
                <li key={ref.host} className={styles.referrerRow}>
                  <span className={styles.referrerHost}>{ref.host}</span>
                  <span className={styles.referrerCount}>{ref.count}</span>
                </li>
              ))}
            </ol>
          </section>
        )}
      </div>
    );
  }

  return (
    <main className={styles.page}>
      <h1 className={styles.heading}>Link Statistics</h1>
      {data && (
        <p className={styles.shortUrl}>
          <a href={data.shortUrl} target="_blank" rel="noopener noreferrer">
            {data.shortUrl}
          </a>
        </p>
      )}
      {renderContent()}
    </main>
  );
}
