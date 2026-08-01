import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { startTransition, useDeferredValue, useEffect, useEffectEvent, useState, type FormEvent } from 'react';
import type {
  BridgeConfigurationUpdate,
  BridgeConfigurationView,
  ProxyRequestLog,
  ProxyRequestSummary,
  ProxyResponseChunk,
} from './types';

const emptyConfig: BridgeConfigurationView = {
  backendName: '',
  backendBaseUrl: '',
  apiKeyHeader: 'Authorization',
  hasApiKey: false,
  defaultHeaders: {},
  connectionString: '',
  recentRequestLimit: 100,
};

export default function App() {
  const [config, setConfig] = useState<BridgeConfigurationView>(emptyConfig);
  const [requests, setRequests] = useState<ProxyRequestSummary[]>([]);
  const [selectedRequestId, setSelectedRequestId] = useState<string | null>(null);
  const [selectedRequest, setSelectedRequest] = useState<ProxyRequestLog | null>(null);
  const [liveFeed, setLiveFeed] = useState<string[]>([]);
  const [draftApiKey, setDraftApiKey] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [connectionState, setConnectionState] = useState('connecting');

  const deferredRequests = useDeferredValue(requests);
  const selectedSummary = deferredRequests.find((request) => request.id === selectedRequestId) ?? null;

  const appendFeed = useEffectEvent((line: string) => {
    startTransition(() => {
      setLiveFeed((current) => [line, ...current].slice(0, 60));
    });
  });

  const refreshConfig = useEffectEvent(async () => {
    const response = await fetch('/api/config');
    const payload = (await response.json()) as BridgeConfigurationView;
    setConfig(payload);
  });

  const refreshRequests = useEffectEvent(async () => {
    const response = await fetch('/api/requests');
    const payload = (await response.json()) as ProxyRequestSummary[];
    setRequests(payload);
    if (!selectedRequestId && payload.length > 0) {
      setSelectedRequestId(payload[0].id);
    }
  });

  const refreshRequestDetail = useEffectEvent(async (requestId: string) => {
    const response = await fetch(`/api/requests/${requestId}`);
    if (!response.ok) {
      return;
    }

    const payload = (await response.json()) as ProxyRequestLog;
    setSelectedRequest(payload);
  });

  useEffect(() => {
    void refreshConfig();
    void refreshRequests();
  }, [refreshConfig, refreshRequests]);

  useEffect(() => {
    if (!selectedRequestId) {
      setSelectedRequest(null);
      return;
    }

    void refreshRequestDetail(selectedRequestId);
  }, [refreshRequestDetail, selectedRequestId]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/bridge')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('requestStarted', (request: ProxyRequestSummary) => {
      appendFeed(`started ${request.method} ${request.path}`);
      startTransition(() => {
        setRequests((current) => [request, ...current.filter((entry) => entry.id !== request.id)].slice(0, 100));
      });
    });

    connection.on('responseChunk', (chunk: ProxyResponseChunk) => {
      appendFeed(`chunk ${chunk.requestId} ${chunk.content.slice(0, 80)}`);
      if (chunk.requestId === selectedRequestId) {
        setSelectedRequest((current) =>
          current
            ? {
                ...current,
                responseBody: `${current.responseBody ?? ''}${chunk.content}`,
              }
            : current,
        );
      }
    });

    connection.on('requestCompleted', (request: ProxyRequestSummary) => {
      appendFeed(`completed ${request.method} ${request.path} (${request.status})`);
      startTransition(() => {
        setRequests((current) => current.map((entry) => (entry.id === request.id ? request : entry)));
      });
      if (request.id === selectedRequestId) {
        void refreshRequestDetail(request.id);
      }
    });

    void connection
      .start()
      .then(() => setConnectionState('connected'))
      .catch(() => setConnectionState('failed'));

    return () => {
      void connection.stop();
    };
  }, [appendFeed, refreshRequestDetail, selectedRequestId]);

  async function saveConfiguration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);

    const update: BridgeConfigurationUpdate = {
      backendName: config.backendName,
      backendBaseUrl: config.backendBaseUrl,
      apiKeyHeader: config.apiKeyHeader,
      apiKey: draftApiKey || undefined,
      connectionString: config.connectionString,
      recentRequestLimit: config.recentRequestLimit,
      defaultHeaders: config.defaultHeaders,
    };

    const response = await fetch('/api/config', {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(update),
    });

    const payload = (await response.json()) as BridgeConfigurationView;
    setConfig(payload);
    setDraftApiKey('');
    setIsSaving(false);
    appendFeed('configuration updated');
  }

  return (
    <div className="shell">
      <header className="hero">
        <div>
          <p className="eyebrow">NilDev.BridgeLM</p>
          <h1>Copilot bridge with live proxy telemetry</h1>
          <p className="lead">
            Inspect request flow, keep upstream credentials controlled in one place, and watch responses arrive over SignalR in real time.
          </p>
        </div>
        <div className={`status-chip status-${connectionState}`}>{connectionState}</div>
      </header>

      <main className="dashboard-grid">
        <section className="panel panel-config">
          <div className="panel-title-row">
            <h2>Runtime configuration</h2>
            <span className="panel-hint">Backed by /api/config</span>
          </div>
          <form className="config-form" onSubmit={saveConfiguration}>
            <label>
              Backend name
              <input value={config.backendName} onChange={(event) => setConfig({ ...config, backendName: event.target.value })} />
            </label>
            <label>
              Backend base URL
              <input value={config.backendBaseUrl} onChange={(event) => setConfig({ ...config, backendBaseUrl: event.target.value })} />
            </label>
            <label>
              API key header
              <input value={config.apiKeyHeader} onChange={(event) => setConfig({ ...config, apiKeyHeader: event.target.value })} />
            </label>
            <label>
              New API key
              <input type="password" value={draftApiKey} onChange={(event) => setDraftApiKey(event.target.value)} placeholder={config.hasApiKey ? 'Stored secret present' : 'No secret stored'} />
            </label>
            <label>
              SQLite connection string
              <input value={config.connectionString} onChange={(event) => setConfig({ ...config, connectionString: event.target.value })} />
            </label>
            <label>
              Recent request limit
              <input
                type="number"
                min={1}
                value={config.recentRequestLimit}
                onChange={(event) => setConfig({ ...config, recentRequestLimit: Number(event.target.value) })}
              />
            </label>
            <button type="submit" disabled={isSaving}>{isSaving ? 'Saving...' : 'Save changes'}</button>
          </form>
        </section>

        <section className="panel panel-requests">
          <div className="panel-title-row">
            <h2>Recent requests</h2>
            <button type="button" className="ghost-button" onClick={() => void refreshRequests()}>Refresh</button>
          </div>
          <div className="request-list">
            {deferredRequests.map((request) => (
              <button
                type="button"
                key={request.id}
                className={request.id === selectedRequestId ? 'request-card selected' : 'request-card'}
                onClick={() => setSelectedRequestId(request.id)}
              >
                <div className="request-headline">
                  <strong>{request.method}</strong>
                  <span>{request.status}</span>
                </div>
                <p>{request.path}</p>
                <small>{request.backendName} · {request.durationMs ?? 0} ms</small>
              </button>
            ))}
          </div>
        </section>

        <section className="panel panel-detail">
          <div className="panel-title-row">
            <h2>Request detail</h2>
            <span className="panel-hint">{selectedSummary?.id ?? 'No selection'}</span>
          </div>
          {selectedRequest ? (
            <div className="detail-stack">
              <article>
                <h3>Request body</h3>
                <pre>{selectedRequest.requestBody}</pre>
              </article>
              <article>
                <h3>Response body</h3>
                <pre>{selectedRequest.responseBody ?? 'Awaiting response...'}</pre>
              </article>
            </div>
          ) : (
            <p className="empty-state">Select a request to inspect the captured payloads.</p>
          )}
        </section>

        <section className="panel panel-live">
          <div className="panel-title-row">
            <h2>Live feed</h2>
            <span className="panel-hint">SignalR event stream</span>
          </div>
          <div className="feed-list">
            {liveFeed.length === 0 ? <p className="empty-state">Waiting for proxied traffic.</p> : null}
            {liveFeed.map((line, index) => (
              <div key={`${line}-${index}`} className="feed-line">{line}</div>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}
