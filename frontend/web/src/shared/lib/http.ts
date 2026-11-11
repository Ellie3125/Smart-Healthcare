import { apiClient } from '../../api/client';
import { ApiHttpError, ProblemDetails } from './errors';
import { createIdempotencyKey } from './idempotency';

export interface RequestOptions {
  params?: Record<string, unknown>;
  idempotencyKey?: string;
  version?: number;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

type SessionReplacedListener = () => void;
const sessionReplacedListeners = new Set<SessionReplacedListener>();

export function onRoomSessionReplaced(listener: SessionReplacedListener): () => void {
  sessionReplacedListeners.add(listener);
  return () => sessionReplacedListeners.delete(listener);
}

function buildHeaders(options?: RequestOptions, includeIdempotency: boolean = false): Record<string, string> {
  const headers: Record<string, string> = { ...(options?.headers ?? {}) };
  if (includeIdempotency) {
    headers['Idempotency-Key'] = options?.idempotencyKey ?? createIdempotencyKey();
  } else if (options?.idempotencyKey) {
    headers['Idempotency-Key'] = options.idempotencyKey;
  }
  if (options?.version !== undefined) {
    headers['If-Match'] = String(options.version);
  }
  return headers;
}

function rethrowNormalized(err: unknown): never {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const resp = (err as { response?: { status?: number; data?: ProblemDetails } }).response;
    const status = resp?.status ?? 500;
    const problem = resp?.data;
    const code = (problem?.code || (status === 409 ? 'VERSION_CONFLICT' : 'HTTP_ERROR')).toUpperCase();
    if (code === 'ROOM_SESSION_REPLACED' || code === 'ROOM_SESSION_CONFLICT') {
      sessionReplacedListeners.forEach((fn) => fn());
    }
    throw new ApiHttpError(
      status,
      code,
      problem?.detail || problem?.title || `Lỗi máy chủ (${status})`,
      problem,
    );
  }
  throw err;
}

export async function apiGet<T>(url: string, options?: RequestOptions): Promise<T> {
  try {
    const res = await apiClient.get<T>(url, {
      params: options?.params,
      headers: buildHeaders(options, false),
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiPost<TResponse, TBody = unknown>(
  url: string,
  body?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.post<TResponse>(url, body, {
      params: options?.params,
      headers: buildHeaders(options, true),
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiPut<TResponse, TBody = unknown>(
  url: string,
  body?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.put<TResponse>(url, body, {
      params: options?.params,
      headers: buildHeaders(options, true),
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiPatch<TResponse, TBody = unknown>(
  url: string,
  body?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.patch<TResponse>(url, body, {
      params: options?.params,
      headers: buildHeaders(options, true),
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiDelete<TResponse = void>(url: string, options?: RequestOptions): Promise<TResponse> {
  try {
    const res = await apiClient.delete<TResponse>(url, {
      params: options?.params,
      headers: buildHeaders(options, false),
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiUpload<TResponse>(
  url: string,
  formData: FormData,
  options?: RequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.post<TResponse>(url, formData, {
      headers: {
        ...buildHeaders(options, true),
        'Content-Type': 'multipart/form-data',
      },
      signal: options?.signal,
    });
    return res.data;
  } catch (err) {
    return rethrowNormalized(err);
  }
}

export async function apiDownloadBlob(url: string): Promise<{ blob: Blob; contentType: string }> {
  try {
    const res = await apiClient.get<Blob>(url, { responseType: 'blob' });
    return {
      blob: res.data,
      contentType: (res.headers['content-type'] as string) || 'application/octet-stream',
    };
  } catch (err) {
    return rethrowNormalized(err);
  }
}
