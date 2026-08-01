export type BridgeConfigurationView = {
  backendName: string;
  backendBaseUrl: string;
  apiKeyHeader: string;
  hasApiKey: boolean;
  defaultHeaders: Record<string, string>;
  connectionString: string;
  recentRequestLimit: number;
};

export type BridgeConfigurationUpdate = {
  backendName?: string;
  backendBaseUrl?: string;
  apiKeyHeader?: string;
  apiKey?: string;
  defaultHeaders?: Record<string, string>;
  connectionString?: string;
  recentRequestLimit?: number;
};

export type ProxyRequestSummary = {
  id: string;
  method: string;
  path: string;
  startedAtUtc: string;
  status: string;
  backendName: string;
  responseStatusCode?: number;
  durationMs?: number;
};

export type ProxyRequestLog = ProxyRequestSummary & {
  queryString: string;
  requestHeaders: string;
  requestBody: string;
  backendUrl: string;
  completedAtUtc?: string;
  responseHeaders?: string;
  responseBody?: string;
  error?: string;
};

export type ProxyResponseChunk = {
  requestId: string;
  content: string;
  timestampUtc: string;
};
