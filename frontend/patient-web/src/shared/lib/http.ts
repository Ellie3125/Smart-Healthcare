import { apiClient } from '../../api/client';
import { PatientApiError, ProblemDetails } from './errors';
import { createIdempotencyKey } from './money';

export interface PatientRequestOptions {
  params?: Record<string, unknown>;
  idempotencyKey?: string;
  version?: number;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

function buildHeaders(opts?: PatientRequestOptions, forceIdempotency: boolean = false): Record<string, string> {
  const headers: Record<string, string> = { ...(opts?.headers ?? {}) };
  if (forceIdempotency) {
    headers['Idempotency-Key'] = opts?.idempotencyKey ?? createIdempotencyKey();
  } else if (opts?.idempotencyKey) {
    headers['Idempotency-Key'] = opts.idempotencyKey;
  }
  if (opts?.version !== undefined) {
    headers['If-Match'] = String(opts.version);
  }
  return headers;
}

function normalizeThrow(err: unknown): never {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const resp = (err as { response?: { status?: number; data?: ProblemDetails } }).response;
    const status = resp?.status ?? 500;
    const problem = resp?.data;
    const code = (problem?.code || `HTTP_${status}`).toUpperCase();
    throw new PatientApiError(
      status,
      code,
      problem?.detail || problem?.title || `Không thể xử lý yêu cầu (${status})`,
      problem,
    );
  }
  throw err;
}

export async function patientGet<T>(url: string, opts?: PatientRequestOptions): Promise<T> {
  try {
    const res = await apiClient.get<T>(url, {
      params: opts?.params,
      headers: buildHeaders(opts, false),
      signal: opts?.signal,
    });
    return res.data;
  } catch (err) {
    return normalizeThrow(err);
  }
}

export async function patientPost<TResponse, TBody = unknown>(
  url: string,
  body?: TBody,
  opts?: PatientRequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.post<TResponse>(url, body, {
      params: opts?.params,
      headers: buildHeaders(opts, true),
      signal: opts?.signal,
    });
    return res.data;
  } catch (err) {
    return normalizeThrow(err);
  }
}

export async function patientPut<TResponse, TBody = unknown>(
  url: string,
  body?: TBody,
  opts?: PatientRequestOptions,
): Promise<TResponse> {
  try {
    const res = await apiClient.put<TResponse>(url, body, {
      params: opts?.params,
      headers: buildHeaders(opts, true),
      signal: opts?.signal,
    });
    return res.data;
  } catch (err) {
    return normalizeThrow(err);
  }
}

/**
 * Tải tệp xét nghiệm/chẩn đoán qua luồng nhị phân của Backend, không lưu hay phơi bày URL lưu trữ trực tiếp.
 */
export async function patientDownloadBlob(url: string): Promise<{ blob: Blob; contentType: string; fileName: string }> {
  try {
    const res = await apiClient.get<Blob>(url, { responseType: 'blob' });
    const disposition = (res.headers['content-disposition'] as string | undefined) || '';
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    const fileName = match ? decodeURIComponent(match[1]) : 'ket-qua-can-lam-sang';
    return {
      blob: res.data,
      contentType: (res.headers['content-type'] as string) || 'application/octet-stream',
      fileName,
    };
  } catch (err) {
    return normalizeThrow(err);
  }
}
