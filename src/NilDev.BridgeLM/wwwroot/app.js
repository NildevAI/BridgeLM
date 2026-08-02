const emptyConfig = {
  backendName: '',
  backendBaseUrl: '',
  apiKeyHeader: 'Authorization',
  hasApiKey: false,
  defaultHeaders: {},
  connectionString: '',
  recentRequestLimit: 100,
};

function createManageDraft() {
  return {
    backendName: '',
    backendBaseUrl: '',
    apiKeyHeader: 'Authorization',
    connectionString: '',
    recentRequestLimit: 100,
  };
}

function createManageValidationState() {
  return {
    formErrors: [],
    fieldErrors: {},
  };
}

class ApiRequestError extends Error {
  constructor(message, { formErrors = [], fieldErrors = {} } = {}) {
    super(message);
    this.name = 'ApiRequestError';
    this.formErrors = formErrors;
    this.fieldErrors = fieldErrors;
  }
}

const state = {
  route: normalizeRoute(window.location.pathname),
  config: { ...emptyConfig },
  requests: [],
  selectedRequestId: null,
  selectedRequest: null,
  isRequestDetailOpen: false,
  isRequestDetailLoading: false,
  isDeletingRequest: false,
  isTruncatingRequests: false,
  liveFeed: [],
  draftApiKey: '',
  isSaving: false,
  connectionState: 'connecting',
  namedConfigs: [],
  selectedConfigName: null,
  selectedConfig: null,
  manageDraft: createManageDraft(),
  manageValidation: createManageValidationState(),
  isConfigSwitcherOpen: false,
  configSwitcherQuery: '',
  configSwitcherFocusIndex: -1,
  manageDraftApiKey: '',
  isManageSaving: false,
};

const elements = {
  navButtons: Array.from(document.querySelectorAll('[data-route]')),
  dashboardView: document.getElementById('dashboard-view'),
  manageView: document.getElementById('manage-view'),
  configSwitcher: document.getElementById('config-switcher'),
  configSwitcherTrigger: document.getElementById('config-switcher-trigger'),
  configSwitcherValue: document.getElementById('config-switcher-value'),
  configSwitcherPanel: document.getElementById('config-switcher-panel'),
  configSwitcherSearch: document.getElementById('config-switcher-search'),
  configSwitcherResults: document.getElementById('config-switcher-results'),
  connectionState: document.getElementById('connection-state'),
  refreshRequests: document.getElementById('refresh-requests'),
  truncateRequests: document.getElementById('truncate-requests'),
  requestList: document.getElementById('request-list'),
  requestDetail: document.getElementById('request-detail'),
  requestDetailModal: document.getElementById('request-detail-modal'),
  requestDetailDialog: document.querySelector('.request-detail-dialog'),
  requestDetailMeta: document.getElementById('request-detail-meta'),
  closeRequestDetail: document.getElementById('close-request-detail'),
  deleteRequest: document.getElementById('delete-request'),
  feedList: document.getElementById('feed-list'),
  refreshConfigs: document.getElementById('refresh-configs'),
  createConfigButton: document.getElementById('create-config-button'),
  configList: document.getElementById('config-list'),
  manageConfigForm: document.getElementById('manage-config-form'),
  manageFormError: document.getElementById('manage-form-error'),
  manageConfigMeta: document.getElementById('manage-config-meta'),
  manageConfigBadges: document.getElementById('manage-config-badges'),
  manageBackendName: document.getElementById('manage-backend-name'),
  manageBackendNameError: document.getElementById('manage-backend-name-error'),
  manageBackendBaseUrl: document.getElementById('manage-backend-base-url'),
  manageBackendBaseUrlError: document.getElementById('manage-backend-base-url-error'),
  manageApiKeyHeader: document.getElementById('manage-api-key-header'),
  manageApiKeyHeaderError: document.getElementById('manage-api-key-header-error'),
  manageApiKey: document.getElementById('manage-api-key'),
  manageApiKeyError: document.getElementById('manage-api-key-error'),
  manageConnectionString: document.getElementById('manage-connection-string'),
  manageConnectionStringError: document.getElementById('manage-connection-string-error'),
  manageRecentRequestLimit: document.getElementById('manage-recent-request-limit'),
  manageRecentRequestLimitError: document.getElementById('manage-recent-request-limit-error'),
  manageSaveButton: document.getElementById('manage-save-button'),
  manageSelectButton: document.getElementById('manage-select-button'),
  manageRenameButton: document.getElementById('manage-rename-button'),
  manageDuplicateButton: document.getElementById('manage-duplicate-button'),
  manageDeleteButton: document.getElementById('manage-delete-button'),
};

function normalizeRoute(pathname) {
  if (pathname === '/configs') {
    return '/configs';
  }

  return '/';
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function selectedSummary() {
  return state.requests.find((request) => request.id === state.selectedRequestId) ?? null;
}

function hasRequestSelection() {
  return Boolean(state.selectedRequestId);
}

function formatPreciseTimestamp(value) {
  if (!value) {
    return 'unknown';
  }

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return 'unknown';
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    fractionalSecondDigits: 3,
  }).format(timestamp);
}

function formatDuration(durationMs) {
  return durationMs == null ? 'pending' : `${durationMs} ms`;
}

function renderRequestDetailMeta(summary, detail) {
  if (!summary && !detail) {
    return 'No request selected.';
  }

  const requestId = detail?.id ?? summary?.id ?? 'unknown';
  const startedAt = formatPreciseTimestamp(detail?.startedAtUtc ?? summary?.startedAtUtc);
  const completedAt = formatPreciseTimestamp(detail?.completedAtUtc);
  const duration = formatDuration(detail?.durationMs ?? summary?.durationMs);
  const status = detail?.status ?? summary?.status ?? 'Pending';

  return `Request ${requestId} · ${status} · started ${startedAt} · completed ${completedAt} · duration ${duration}`;
}

function renderRequestDetailModal() {
  elements.requestDetailModal.hidden = !state.isRequestDetailOpen;
  elements.requestDetailModal.setAttribute('aria-hidden', state.isRequestDetailOpen ? 'false' : 'true');
  document.body.classList.toggle('modal-open', state.isRequestDetailOpen);
}

function setRequestDetailOpen(isOpen) {
  state.isRequestDetailOpen = isOpen && hasRequestSelection();
  renderRequestDetailModal();

  if (state.isRequestDetailOpen) {
    window.requestAnimationFrame(() => {
      elements.requestDetailDialog.focus();
    });
    return;
  }

  const trigger = state.selectedRequestId
    ? elements.requestList.querySelector(`[data-request-id="${CSS.escape(state.selectedRequestId)}"]`)
    : null;

  if (trigger instanceof HTMLElement) {
    window.requestAnimationFrame(() => {
      trigger.focus();
    });
  }
}

function clearSelectedRequest({ closeModal = false } = {}) {
  state.selectedRequestId = null;
  state.selectedRequest = null;
  state.isRequestDetailLoading = false;
  if (closeModal) {
    state.isRequestDetailOpen = false;
    renderRequestDetailModal();
  }
}

function removeRequestFromState(requestId) {
  state.requests = state.requests.filter((request) => request.id !== requestId);

  if (state.selectedRequestId === requestId) {
    state.selectedRequestId = state.requests[0]?.id ?? null;
    state.selectedRequest = null;
    state.isRequestDetailLoading = false;
  }
}

function appendFeed(line) {
  state.liveFeed = [line, ...state.liveFeed].slice(0, 60);
  renderLiveFeed();
}

function setConnectionState(nextState) {
  state.connectionState = nextState;
  elements.connectionState.textContent = nextState;
  elements.connectionState.className = `status-chip status-${nextState}`;
}

function getManageValidationElements() {
  return {
    name: {
      input: elements.manageBackendName,
      error: elements.manageBackendNameError,
    },
    backendBaseUrl: {
      input: elements.manageBackendBaseUrl,
      error: elements.manageBackendBaseUrlError,
    },
    apiKeyHeader: {
      input: elements.manageApiKeyHeader,
      error: elements.manageApiKeyHeaderError,
    },
    apiKey: {
      input: elements.manageApiKey,
      error: elements.manageApiKeyError,
    },
    connectionString: {
      input: elements.manageConnectionString,
      error: elements.manageConnectionStringError,
    },
    recentRequestLimit: {
      input: elements.manageRecentRequestLimit,
      error: elements.manageRecentRequestLimitError,
    },
  };
}

function normalizeValidationMessages(messages) {
  if (!messages) {
    return [];
  }

  return Array.isArray(messages)
    ? messages.filter(Boolean).map((message) => String(message))
    : [String(messages)];
}

function createManagedValidationState(validation = {}) {
  const formErrors = normalizeValidationMessages(validation.formErrors);
  const fieldErrors = Object.fromEntries(
    Object.entries(validation.fieldErrors ?? {})
      .map(([fieldName, messages]) => [fieldName, normalizeValidationMessages(messages)])
      .filter(([, messages]) => messages.length > 0),
  );

  return { formErrors, fieldErrors };
}

function hasManagedValidationErrors(validation) {
  return validation.formErrors.length > 0 || Object.keys(validation.fieldErrors).length > 0;
}

function setManageValidation(validation = {}) {
  state.manageValidation = createManagedValidationState(validation);
  renderManageValidation();
}

function clearManageValidation(fieldName) {
  if (!fieldName) {
    state.manageValidation = createManageValidationState();
    renderManageValidation();
    return;
  }

  const nextFieldErrors = { ...state.manageValidation.fieldErrors };
  delete nextFieldErrors[fieldName];
  state.manageValidation = {
    formErrors: state.manageValidation.formErrors,
    fieldErrors: nextFieldErrors,
  };
  renderManageValidation();
}

function renderValidationMessages(target, messages) {
  const normalizedMessages = normalizeValidationMessages(messages);
  target.hidden = normalizedMessages.length === 0;
  target.textContent = normalizedMessages.join(' ');
}

function renderManageValidation() {
  renderValidationMessages(elements.manageFormError, state.manageValidation.formErrors);

  const validationElements = getManageValidationElements();
  Object.entries(validationElements).forEach(([fieldName, mapping]) => {
    const messages = state.manageValidation.fieldErrors[fieldName] ?? [];
    renderValidationMessages(mapping.error, messages);
    mapping.input.classList.toggle('input-invalid', messages.length > 0);
    mapping.input.setAttribute('aria-invalid', messages.length > 0 ? 'true' : 'false');
  });
}

function updateManageDraft(fieldName, value) {
  if (state.selectedConfig) {
    return;
  }

  state.manageDraft = {
    ...state.manageDraft,
    [fieldName]: value,
  };
}

function validateManageConfiguration() {
  const name = getManagedConfigName();
  const recentRequestLimit = Number(elements.manageRecentRequestLimit.value);
  const validation = createManageValidationState();

  if (!name) {
    validation.fieldErrors.name = ['Proxy configuration name is required.'];
  } else if (/[/\\]/.test(name)) {
    validation.fieldErrors.name = ['Proxy configuration names cannot contain path separators.'];
  }

  if (!Number.isInteger(recentRequestLimit) || recentRequestLimit <= 0) {
    validation.fieldErrors.recentRequestLimit = ['Recent request limit must be a positive integer.'];
  }

  return validation;
}

function getErrorMessage(error) {
  return error instanceof Error ? error.message : String(error ?? 'Request failed.');
}

function mapManageOperationError(error) {
  if (error instanceof ApiRequestError) {
    const validation = createManagedValidationState({
      formErrors: error.formErrors,
      fieldErrors: error.fieldErrors,
    });

    if (hasManagedValidationErrors(validation)) {
      return validation;
    }
  }

  const message = getErrorMessage(error);
  if (message.includes('name is required')) {
    return createManagedValidationState({ fieldErrors: { name: [message] } });
  }

  if (message.includes('path separators') || message.includes('already exists')) {
    return createManagedValidationState({ fieldErrors: { name: [message] } });
  }

  return createManagedValidationState({ formErrors: [message] });
}

function renderRoute() {
  elements.dashboardView.hidden = state.route !== '/';
  elements.manageView.hidden = state.route !== '/configs';

  elements.navButtons.forEach((button) => {
    button.classList.toggle('active', button.dataset.route === state.route);
  });
}

function getActiveNamedConfig() {
  return state.namedConfigs.find((config) => config.isActive) ?? null;
}

function getConfigSwitcherQuery() {
  return state.configSwitcherQuery.trim().toLowerCase();
}

function getFilteredNamedConfigs() {
  const query = getConfigSwitcherQuery();
  if (!query) {
    return state.namedConfigs;
  }

  return state.namedConfigs.filter((config) => [config.name, config.backendName, config.backendBaseUrl]
    .some((value) => String(value ?? '').toLowerCase().includes(query)));
}

function setConfigSwitcherOpen(isOpen, { focusSearch = false, clearQuery = false, restoreFocus = false } = {}) {
  const nextOpen = isOpen && state.namedConfigs.length > 0;
  const shouldClearQuery = clearQuery && state.configSwitcherQuery;
  if (state.isConfigSwitcherOpen === nextOpen && !shouldClearQuery) {
    if (!nextOpen && restoreFocus) {
      window.requestAnimationFrame(() => {
        elements.configSwitcherTrigger.focus();
      });
    }

    return;
  }

  if (clearQuery) {
    state.configSwitcherQuery = '';
  }

  state.isConfigSwitcherOpen = nextOpen;
  if (!state.isConfigSwitcherOpen) {
    state.configSwitcherFocusIndex = -1;
  }

  renderConfigSwitcher();

  if (state.isConfigSwitcherOpen && focusSearch) {
    window.requestAnimationFrame(() => {
      elements.configSwitcherSearch.focus();
      elements.configSwitcherSearch.select();
    });
  }

  if (!state.isConfigSwitcherOpen && restoreFocus) {
    window.requestAnimationFrame(() => {
      elements.configSwitcherTrigger.focus();
    });
  }
}

function moveConfigSwitcherFocus(delta) {
  const filteredConfigs = getFilteredNamedConfigs();
  if (filteredConfigs.length === 0) {
    state.configSwitcherFocusIndex = -1;
    renderConfigSwitcher();
    return;
  }

  if (state.configSwitcherFocusIndex < 0) {
    state.configSwitcherFocusIndex = 0;
  } else {
    state.configSwitcherFocusIndex = (state.configSwitcherFocusIndex + delta + filteredConfigs.length) % filteredConfigs.length;
  }

  renderConfigSwitcher();
}

async function activateConfigFromSwitcher(name) {
  setConfigSwitcherOpen(false, { clearQuery: true });
  await activateNamedConfiguration(name);
}

function renderConfigSwitcher() {
  const activeConfig = getActiveNamedConfig();
  const filteredConfigs = getFilteredNamedConfigs();
  const isDisabled = state.namedConfigs.length === 0;

  if (isDisabled) {
    state.isConfigSwitcherOpen = false;
    state.configSwitcherQuery = '';
    state.configSwitcherFocusIndex = -1;
  } else if (filteredConfigs.length === 0) {
    state.configSwitcherFocusIndex = -1;
  } else if (state.configSwitcherFocusIndex < 0 || state.configSwitcherFocusIndex >= filteredConfigs.length) {
    const activeIndex = filteredConfigs.findIndex((config) => config.isActive);
    state.configSwitcherFocusIndex = activeIndex >= 0 ? activeIndex : 0;
  }

  elements.configSwitcher.classList.toggle('open', state.isConfigSwitcherOpen);
  elements.configSwitcher.classList.toggle('disabled', isDisabled);
  elements.configSwitcherTrigger.disabled = isDisabled;
  elements.configSwitcherTrigger.setAttribute('aria-expanded', String(state.isConfigSwitcherOpen));
  elements.configSwitcherPanel.hidden = !state.isConfigSwitcherOpen || isDisabled;
  elements.configSwitcherSearch.value = state.configSwitcherQuery;
  elements.configSwitcherSearch.disabled = isDisabled;
  elements.configSwitcherValue.textContent = activeConfig?.name ?? 'No saved configs';

  if (isDisabled) {
    elements.configSwitcherResults.innerHTML = '<p class="config-switcher-empty">Save a config to make it available here.</p>';
    return;
  }

  if (filteredConfigs.length === 0) {
    elements.configSwitcherResults.innerHTML = '<p class="config-switcher-empty">No configs match this search.</p>';
    return;
  }

  elements.configSwitcherResults.innerHTML = filteredConfigs
    .map((config, index) => {
      const optionClasses = [
        'config-switcher-option',
        config.isActive ? 'active' : '',
        index === state.configSwitcherFocusIndex ? 'focused' : '',
      ].filter(Boolean).join(' ');

      return `
        <button
          type="button"
          id="config-switcher-option-${index}"
          class="${optionClasses}"
          role="option"
          aria-selected="${config.isActive ? 'true' : 'false'}"
          data-config-name="${escapeHtml(config.name)}"
        >
          <span class="config-switcher-option-copy">
            <span class="config-switcher-option-name">${escapeHtml(config.name)}</span>
            <span class="config-switcher-option-meta">${escapeHtml(config.backendName || config.backendBaseUrl || 'Saved config')}</span>
          </span>
          ${config.isActive ? '<span class="config-switcher-option-badge">Active</span>' : ''}
        </button>`;
    })
    .join('');

  if (state.isConfigSwitcherOpen && state.configSwitcherFocusIndex >= 0) {
    const options = elements.configSwitcherResults.querySelectorAll('[data-config-name]');
    const focusedOption = options.item(state.configSwitcherFocusIndex);
    if (focusedOption instanceof HTMLElement) {
      focusedOption.scrollIntoView({ block: 'nearest' });
    }
  }
}

function renderRequests() {
  if (state.requests.length === 0) {
    elements.requestList.innerHTML = '<p class="empty-state">No proxied requests captured yet.</p>';
    return;
  }

  elements.requestList.innerHTML = state.requests
    .map((request) => {
      const selectedClass = request.id === state.selectedRequestId ? 'request-card selected' : 'request-card';
      return `
        <button type="button" class="${selectedClass}" data-request-id="${escapeHtml(request.id)}">
          <div class="request-headline">
            <strong>${escapeHtml(request.method)}</strong>
            <span>${escapeHtml(request.status)}</span>
          </div>
          <p>${escapeHtml(request.path)}</p>
          <small class="request-timestamp">${escapeHtml(formatPreciseTimestamp(request.startedAtUtc))}</small>
          <small>${escapeHtml(request.backendName)} · ${escapeHtml(formatDuration(request.durationMs))}</small>
        </button>`;
    })
    .join('');
}

function renderRequestDetail() {
  const summary = selectedSummary();
  elements.requestDetailMeta.textContent = renderRequestDetailMeta(summary, state.selectedRequest);
  elements.deleteRequest.disabled = !summary || state.isDeletingRequest || state.isTruncatingRequests;
  elements.deleteRequest.textContent = state.isDeletingRequest ? 'Deleting...' : 'Delete request';
  elements.requestDetail.setAttribute('aria-busy', state.isRequestDetailLoading ? 'true' : 'false');

  if (state.isRequestDetailLoading && summary) {
    elements.requestDetail.innerHTML = '<p class="empty-state">Loading request detail...</p>';
    return;
  }

  if (!state.selectedRequest) {
    elements.requestDetail.innerHTML = '<p class="empty-state">Select a request to inspect the captured payloads.</p>';
    return;
  }

  elements.requestDetail.innerHTML = `
    <div class="detail-stack">
      <article class="detail-summary-card">
        <h3>Request metadata</h3>
        <dl class="detail-metadata-grid">
          <div>
            <dt>Request ID</dt>
            <dd>${escapeHtml(state.selectedRequest.id)}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd>${escapeHtml(state.selectedRequest.status)}</dd>
          </div>
          <div>
            <dt>Backend</dt>
            <dd>${escapeHtml(state.selectedRequest.backendName)}</dd>
          </div>
          <div>
            <dt>Started</dt>
            <dd>${escapeHtml(formatPreciseTimestamp(state.selectedRequest.startedAtUtc))}</dd>
          </div>
          <div>
            <dt>Completed</dt>
            <dd>${escapeHtml(formatPreciseTimestamp(state.selectedRequest.completedAtUtc))}</dd>
          </div>
          <div>
            <dt>Duration</dt>
            <dd>${escapeHtml(formatDuration(state.selectedRequest.durationMs))}</dd>
          </div>
          <div>
            <dt>Response code</dt>
            <dd>${escapeHtml(state.selectedRequest.responseStatusCode ?? 'pending')}</dd>
          </div>
          <div>
            <dt>Path</dt>
            <dd>${escapeHtml(`${state.selectedRequest.path}${state.selectedRequest.queryString ?? ''}`)}</dd>
          </div>
        </dl>
      </article>
      <article>
        <h3>Request body</h3>
        <pre>${escapeHtml(state.selectedRequest.requestBody ?? '')}</pre>
      </article>
      <article>
        <h3>Response body</h3>
        <pre>${escapeHtml(state.selectedRequest.responseBody ?? 'Awaiting response...')}</pre>
      </article>
    </div>`;
}

function renderLiveFeed() {
  if (state.liveFeed.length === 0) {
    elements.feedList.innerHTML = '<p class="empty-state">Waiting for proxied traffic.</p>';
    return;
  }

  elements.feedList.innerHTML = state.liveFeed
    .map((line, index) => `<div class="feed-line" data-feed-index="${index}">${escapeHtml(line)}</div>`)
    .join('');
}

function renderConfigList() {
  if (state.namedConfigs.length === 0) {
    elements.configList.innerHTML = '<p class="empty-state">No saved proxy configs yet.</p>';
    return;
  }

  elements.configList.innerHTML = state.namedConfigs
    .map((configuration) => {
      const badges = renderBadgesMarkup(configuration);
      const selectedClass = configuration.name === state.selectedConfigName ? 'config-tile active' : 'config-tile';
      return `
        <button type="button" class="${selectedClass}" data-config-name="${escapeHtml(configuration.name)}">
          <div class="config-tile-head">
            <div>
              <h3>${escapeHtml(configuration.name)}</h3>
              <p>${escapeHtml(configuration.backendBaseUrl)}</p>
            </div>
            <div class="badge-row">${badges}</div>
          </div>
          <small>Recent limit ${escapeHtml(configuration.recentRequestLimit)}</small>
        </button>`;
    })
    .join('');
}

function renderManageEditor() {
  const configuration = state.selectedConfig;
  if (!configuration) {
    elements.manageBackendName.value = state.manageDraft.backendName;
    elements.manageBackendBaseUrl.value = state.manageDraft.backendBaseUrl;
    elements.manageApiKeyHeader.value = state.manageDraft.apiKeyHeader;
    elements.manageApiKey.value = state.manageDraftApiKey;
    elements.manageApiKey.placeholder = 'Optional secret for this saved config';
    elements.manageConnectionString.value = state.manageDraft.connectionString;
    elements.manageRecentRequestLimit.value = String(state.manageDraft.recentRequestLimit);
    elements.manageConfigMeta.textContent = 'Create a new saved config with explicit values instead of inheriting the active runtime settings.';
    elements.manageConfigBadges.innerHTML = '';
    setManageActionState(false);
    elements.manageSaveButton.disabled = state.isManageSaving;
    elements.manageSaveButton.textContent = state.isManageSaving ? 'Saving...' : 'Create config';
    renderManageValidation();
    return;
  }

  elements.manageBackendName.value = configuration.name;
  elements.manageBackendBaseUrl.value = configuration.configuration.backendBaseUrl;
  elements.manageApiKeyHeader.value = configuration.configuration.apiKeyHeader;
  elements.manageApiKey.value = state.manageDraftApiKey;
  elements.manageApiKey.placeholder = configuration.configuration.hasApiKey ? 'Stored secret present' : 'No secret stored';
  elements.manageConnectionString.value = configuration.configuration.connectionString;
  elements.manageRecentRequestLimit.value = String(configuration.configuration.recentRequestLimit);
  elements.manageConfigMeta.textContent = `Created ${formatDate(configuration.createdAtUtc)} · updated ${formatDate(configuration.updatedAtUtc)}`;
  elements.manageConfigBadges.innerHTML = renderBadgesMarkup(configuration);
  setManageActionState(true);
  elements.manageSaveButton.disabled = state.isManageSaving;
  elements.manageSaveButton.textContent = state.isManageSaving ? 'Saving...' : 'Save config';
  renderManageValidation();
}

function setManageActionState(hasSelection) {
  elements.manageSelectButton.disabled = !hasSelection;
  elements.manageRenameButton.disabled = !hasSelection;
  elements.manageDuplicateButton.disabled = !hasSelection;
  elements.manageDeleteButton.disabled = !hasSelection;
}

function renderBadgesMarkup(configuration) {
  return [
    configuration.isActive ? '<span class="badge badge-active">Active</span>' : '',
  ].join('');
}

function render() {
  renderRoute();
  renderConfigSwitcher();
  renderRequests();
  renderRequestDetail();
  renderRequestDetailModal();
  renderLiveFeed();
  renderConfigList();
  renderManageEditor();
}

async function apiFetch(url, options = {}) {
  const response = await fetch(url, options);
  if (response.ok) {
    return response;
  }

  let detail = `Request failed with status ${response.status}.`;
  let formErrors = [];
  let fieldErrors = {};
  try {
    const payload = await response.json();
    detail = payload.detail ?? payload.error ?? detail;
    formErrors = normalizeValidationMessages(payload.formErrors);
    fieldErrors = payload.fieldErrors ?? {};
  } catch {
    detail = (await response.text()) || detail;
  }

  throw new ApiRequestError(detail, { formErrors, fieldErrors });
}

async function apiGetJson(url) {
  const response = await apiFetch(url);
  return response.json();
}

async function apiGetJsonWithBody(url, options) {
  const response = await apiFetch(url, {
    headers: {
      'Content-Type': 'application/json',
    },
    ...options,
  });

  return response.json();
}

async function refreshConfig() {
  state.config = await apiGetJson('/api/config');
}

async function refreshRequests() {
  state.requests = await apiGetJson('/api/requests');
  if (state.selectedRequestId && !state.requests.some((request) => request.id === state.selectedRequestId)) {
    clearSelectedRequest({ closeModal: true });
  }

  if (!state.selectedRequestId && state.requests.length > 0) {
    state.selectedRequestId = state.requests[0].id;
  }

  renderRequests();
  renderRequestDetail();
}

async function refreshRequestDetail(requestId) {
  state.isRequestDetailLoading = true;
  renderRequestDetail();

  try {
    state.selectedRequest = await apiGetJson(`/api/requests/${encodeURIComponent(requestId)}`);
  } catch {
    state.selectedRequest = null;
  } finally {
    state.isRequestDetailLoading = false;
    renderRequestDetail();
  }
}

async function refreshNamedConfigs(preferredName = state.selectedConfigName) {
  state.namedConfigs = await apiGetJson('/api/configs');
  if (state.namedConfigs.length === 0) {
    state.selectedConfigName = null;
    state.selectedConfig = null;
    state.manageDraft = createManageDraft();
    clearManageValidation();
    render();
    return;
  }

  const preferred = state.namedConfigs.find((configuration) => configuration.name === preferredName)?.name;
  const fallback = state.namedConfigs.find((configuration) => configuration.isActive)?.name ?? state.namedConfigs[0].name;
  await loadNamedConfig(preferred ?? fallback);
}

async function loadNamedConfig(name) {
  state.selectedConfigName = name;
  state.manageDraftApiKey = '';
  clearManageValidation();
  state.selectedConfig = await apiGetJson(`/api/configs/${encodeURIComponent(name)}`);
  render();
}

async function selectRequest(requestId) {
  state.selectedRequestId = requestId;
  state.selectedRequest = null;
  state.isRequestDetailLoading = true;
  renderRequests();
  renderRequestDetail();
  await refreshRequestDetail(requestId);
}

async function openRequestDetail(requestId) {
  state.selectedRequestId = requestId;
  setRequestDetailOpen(true);
  await selectRequest(requestId);
}

function closeRequestDetail() {
  setRequestDetailOpen(false);
}

async function deleteRequest(requestId) {
  if (!requestId) {
    return;
  }

  const confirmed = window.confirm(`Delete request ${requestId}? This cannot be undone.`);
  if (!confirmed) {
    return;
  }

  state.isDeletingRequest = true;
  renderRequestDetail();

  try {
    await apiFetch(`/api/requests/${encodeURIComponent(requestId)}`, { method: 'DELETE' });
    removeRequestFromState(requestId);
    closeRequestDetail();
    renderRequests();
    renderRequestDetail();
  } catch (error) {
    window.alert(getErrorMessage(error));
  } finally {
    state.isDeletingRequest = false;
    renderRequestDetail();
  }
}

async function truncateRequests() {
  const confirmed = window.confirm('Delete all stored requests and clear the live feed? This cannot be undone.');
  if (!confirmed) {
    return;
  }

  state.isTruncatingRequests = true;
  elements.truncateRequests.disabled = true;
  elements.truncateRequests.textContent = 'Truncating...';

  try {
    await apiFetch('/api/requests', { method: 'DELETE' });
    clearSelectedRequest({ closeModal: true });
    state.requests = [];
    state.liveFeed = [];
    render();
  } catch (error) {
    window.alert(getErrorMessage(error));
  } finally {
    state.isTruncatingRequests = false;
    elements.truncateRequests.disabled = false;
    elements.truncateRequests.textContent = 'Truncate all';
    renderRequestDetail();
  }
}

function collectManagePayload() {
  return {
    backendName: elements.manageBackendName.value.trim(),
    backendBaseUrl: elements.manageBackendBaseUrl.value,
    apiKeyHeader: elements.manageApiKeyHeader.value,
    apiKey: state.manageDraftApiKey || undefined,
    connectionString: elements.manageConnectionString.value,
    recentRequestLimit: Number(elements.manageRecentRequestLimit.value),
  };
}

function getManagedConfigName() {
  return elements.manageBackendName.value.trim();
}

async function saveManagedConfiguration(event) {
  event.preventDefault();
  const validation = validateManageConfiguration();
  if (hasManagedValidationErrors(validation)) {
    setManageValidation(validation);
    return;
  }

  state.isManageSaving = true;
  clearManageValidation();
  renderManageEditor();

  try {
    const name = getManagedConfigName();
    const payload = collectManagePayload();

    if (state.selectedConfig && state.selectedConfig.name === state.selectedConfigName) {
      let targetName = state.selectedConfigName;
      if (state.selectedConfig.name !== name) {
        const renamed = await apiGetJsonWithBody(`/api/configs/${encodeURIComponent(state.selectedConfig.name)}/rename`, {
          method: 'POST',
          body: JSON.stringify({ name }),
        });
        state.selectedConfig = renamed;
        state.selectedConfigName = renamed.name;
        targetName = renamed.name;
      }

      const updated = await apiGetJsonWithBody(`/api/configs/${encodeURIComponent(targetName)}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      });
      state.selectedConfig = updated;
      state.selectedConfigName = updated.name;
    } else {
      const created = await apiGetJsonWithBody('/api/configs', {
        method: 'POST',
        body: JSON.stringify({
          name,
          ...payload,
        }),
      });
      state.selectedConfig = created;
      state.selectedConfigName = created.name;
    }

    state.manageDraftApiKey = '';
    clearManageValidation();
    await refreshConfig();
    await refreshNamedConfigs(state.selectedConfigName);
  } catch (error) {
    setManageValidation(mapManageOperationError(error));
  } finally {
    state.isManageSaving = false;
    renderManageEditor();
  }
}

async function activateNamedConfiguration(name) {
  try {
    const selected = await apiGetJsonWithBody(`/api/configs/${encodeURIComponent(name)}/select`, {
      method: 'POST',
      body: '',
    });
    state.selectedConfig = selected;
    state.selectedConfigName = selected.name;
    clearManageValidation();
    await refreshConfig();
    await refreshNamedConfigs(selected.name);
  } catch (error) {
    setManageValidation(mapManageOperationError(error));
  }
}

async function renameSelectedConfiguration() {
  if (!state.selectedConfig) {
    return;
  }

  try {
    const renamed = await apiGetJsonWithBody(`/api/configs/${encodeURIComponent(state.selectedConfig.name)}/rename`, {
      method: 'POST',
      body: JSON.stringify({ name: getManagedConfigName() }),
    });
    state.selectedConfig = renamed;
    state.selectedConfigName = renamed.name;
    clearManageValidation();
    await refreshNamedConfigs(renamed.name);
  } catch (error) {
    setManageValidation(mapManageOperationError(error));
  }
}

async function duplicateSelectedConfiguration() {
  if (!state.selectedConfig) {
    return;
  }

  const targetName = window.prompt('Duplicate saved config as:', `${state.selectedConfig.name} Copy`);
  if (!targetName) {
    return;
  }

  try {
    const duplicated = await apiGetJsonWithBody(`/api/configs/${encodeURIComponent(state.selectedConfig.name)}/duplicate`, {
      method: 'POST',
      body: JSON.stringify({ name: targetName.trim() }),
    });
    state.selectedConfig = duplicated;
    state.selectedConfigName = duplicated.name;
    clearManageValidation();
    await refreshNamedConfigs(duplicated.name);
  } catch (error) {
    setManageValidation(mapManageOperationError(error));
  }
}

async function deleteSelectedConfiguration() {
  if (!state.selectedConfig) {
    return;
  }

  const confirmed = window.confirm(`Delete ${state.selectedConfig.name}? This cannot be undone.`);
  if (!confirmed) {
    return;
  }

  try {
    await apiFetch(`/api/configs/${encodeURIComponent(state.selectedConfig.name)}`, { method: 'DELETE' });
    state.selectedConfig = null;
    state.selectedConfigName = null;
    state.manageDraft = createManageDraft();
    state.manageDraftApiKey = '';
    clearManageValidation();
    await refreshNamedConfigs();
  } catch (error) {
    setManageValidation(mapManageOperationError(error));
  }
}

function beginCreateConfiguration() {
  state.selectedConfig = null;
  state.selectedConfigName = null;
  state.manageDraft = createManageDraft();
  state.manageDraftApiKey = '';
  clearManageValidation();
  renderManageEditor();
}

function formatDate(value) {
  return value ? new Date(value).toLocaleString() : 'unknown';
}

function navigate(route) {
  const nextRoute = normalizeRoute(route);
  if (state.route === nextRoute) {
    return;
  }

  state.route = nextRoute;
  window.history.pushState({}, '', nextRoute);
  renderRoute();
}

function wireInputHandlers() {
  elements.navButtons.forEach((button) => {
    button.addEventListener('click', () => {
      navigate(button.dataset.route ?? '/');
    });
  });

  window.addEventListener('popstate', () => {
    state.route = normalizeRoute(window.location.pathname);
    renderRoute();
  });

  elements.configSwitcherTrigger.addEventListener('click', () => {
    if (state.isConfigSwitcherOpen) {
      setConfigSwitcherOpen(false, { clearQuery: true });
      return;
    }

    setConfigSwitcherOpen(true, { focusSearch: true });
  });

  elements.configSwitcherTrigger.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      setConfigSwitcherOpen(true, { focusSearch: true });
    }
  });

  elements.configSwitcherSearch.addEventListener('input', (event) => {
    state.configSwitcherQuery = event.target.value;
    state.configSwitcherFocusIndex = 0;
    renderConfigSwitcher();
  });

  elements.configSwitcherSearch.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      setConfigSwitcherOpen(false, { clearQuery: true, restoreFocus: true });
      return;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      moveConfigSwitcherFocus(1);
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      moveConfigSwitcherFocus(-1);
      return;
    }

    if (event.key === 'Enter') {
      const filteredConfigs = getFilteredNamedConfigs();
      const focusedConfig = filteredConfigs[state.configSwitcherFocusIndex] ?? filteredConfigs[0];
      if (!focusedConfig) {
        return;
      }

      event.preventDefault();
      void activateConfigFromSwitcher(focusedConfig.name);
    }
  });

  elements.configSwitcherResults.addEventListener('click', (event) => {
    const button = event.target.closest('[data-config-name]');
    if (!(button instanceof HTMLElement)) {
      return;
    }

    const name = button.dataset.configName;
    if (name) {
      void activateConfigFromSwitcher(name);
    }
  });

  document.addEventListener('click', (event) => {
    if (!(event.target instanceof Node)) {
      return;
    }

    if (!elements.configSwitcher.contains(event.target)) {
      setConfigSwitcherOpen(false, { clearQuery: true });
    }
  });

  elements.manageConfigForm.addEventListener('submit', (event) => {
    void saveManagedConfiguration(event);
  });

  elements.refreshRequests.addEventListener('click', () => {
    void refreshRequests();
  });

  elements.truncateRequests.addEventListener('click', () => {
    void truncateRequests();
  });

  elements.refreshConfigs.addEventListener('click', () => {
    void refreshNamedConfigs();
  });

  elements.createConfigButton.addEventListener('click', beginCreateConfiguration);

  elements.manageSelectButton.addEventListener('click', () => {
    if (state.selectedConfigName) {
      void activateNamedConfiguration(state.selectedConfigName);
    }
  });

  elements.manageRenameButton.addEventListener('click', () => {
    void renameSelectedConfiguration();
  });

  elements.manageDuplicateButton.addEventListener('click', () => {
    void duplicateSelectedConfiguration();
  });

  elements.manageDeleteButton.addEventListener('click', () => {
    void deleteSelectedConfiguration();
  });

  elements.requestList.addEventListener('click', (event) => {
    const button = event.target.closest('[data-request-id]');
    if (!(button instanceof HTMLElement)) {
      return;
    }

    const requestId = button.dataset.requestId;
    if (requestId) {
      void openRequestDetail(requestId);
    }
  });

  elements.closeRequestDetail.addEventListener('click', () => {
    closeRequestDetail();
  });

  elements.deleteRequest.addEventListener('click', () => {
    void deleteRequest(state.selectedRequestId);
  });

  elements.requestDetailModal.addEventListener('click', (event) => {
    if (event.target === elements.requestDetailModal) {
      closeRequestDetail();
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && state.isRequestDetailOpen) {
      event.preventDefault();
      closeRequestDetail();
    }
  });

  elements.configList.addEventListener('click', (event) => {
    const button = event.target.closest('[data-config-name]');
    if (!(button instanceof HTMLElement)) {
      return;
    }

    const name = button.dataset.configName;
    if (name) {
      void loadNamedConfig(name);
    }
  });

  elements.manageApiKey.addEventListener('input', () => {
    state.manageDraftApiKey = elements.manageApiKey.value;
    clearManageValidation('apiKey');
  });

  elements.manageBackendName.addEventListener('input', () => {
    updateManageDraft('backendName', elements.manageBackendName.value);
    clearManageValidation('name');
  });

  elements.manageBackendBaseUrl.addEventListener('input', () => {
    updateManageDraft('backendBaseUrl', elements.manageBackendBaseUrl.value);
    clearManageValidation('backendBaseUrl');
  });

  elements.manageApiKeyHeader.addEventListener('input', () => {
    updateManageDraft('apiKeyHeader', elements.manageApiKeyHeader.value);
    clearManageValidation('apiKeyHeader');
  });

  elements.manageConnectionString.addEventListener('input', () => {
    updateManageDraft('connectionString', elements.manageConnectionString.value);
    clearManageValidation('connectionString');
  });

  elements.manageRecentRequestLimit.addEventListener('input', () => {
    updateManageDraft('recentRequestLimit', elements.manageRecentRequestLimit.value);
    clearManageValidation('recentRequestLimit');
  });
}

function wireSignalR() {
  if (!window.signalR) {
    setConnectionState('failed');
    appendFeed('signalr client unavailable');
    return;
  }

  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl('/hubs/bridge')
    .withAutomaticReconnect()
    .configureLogging(window.signalR.LogLevel.Warning)
    .build();

  connection.on('requestStarted', (request) => {
    appendFeed(`started ${request.method} ${request.path}`);
    state.requests = [request, ...state.requests.filter((entry) => entry.id !== request.id)].slice(0, 100);
    renderRequests();
    renderRequestDetail();
  });

  connection.on('responseChunk', (chunk) => {
    appendFeed(`chunk ${chunk.requestId} ${chunk.content.slice(0, 80)}`);
    if (chunk.requestId === state.selectedRequestId && state.selectedRequest) {
      state.selectedRequest = {
        ...state.selectedRequest,
        responseBody: `${state.selectedRequest.responseBody ?? ''}${chunk.content}`,
      };
      renderRequestDetail();
    }
  });

  connection.on('requestCompleted', (request) => {
    appendFeed(`completed ${request.method} ${request.path} (${request.status})`);
    state.requests = state.requests.map((entry) => (entry.id === request.id ? request : entry));
    renderRequests();
    renderRequestDetail();
    if (request.id === state.selectedRequestId) {
      void refreshRequestDetail(request.id);
    }
  });

  connection.onreconnecting(() => {
    setConnectionState('reconnecting');
  });

  connection.onreconnected(() => {
    setConnectionState('connected');
  });

  connection.onclose(() => {
    setConnectionState('failed');
  });

  void connection.start()
    .then(() => {
      setConnectionState('connected');
    })
    .catch(() => {
      setConnectionState('failed');
    });
}

async function initialize() {
  wireInputHandlers();
  render();

  try {
    await refreshConfig();
    await refreshRequests();
    await refreshNamedConfigs();
    if (state.selectedRequestId) {
      await refreshRequestDetail(state.selectedRequestId);
    }
  } catch (error) {
    console.error(error);
  }

  wireSignalR();
}

void initialize();