import { patientGet, patientPost } from '../shared/lib/http';
import type {
  ChatHealthStatus,
  ChatMessageDto,
  ChatSessionResponse,
  PatientNotificationItem,
} from '../types/assistant';

/**
 * Notifications & Chatbot Assistant API (Patient Web).
 * Tham chiếu `docs/api/chatbot.md` (v0.1) + `// TODO: nối contract thật`.
 */
export const assistantApi = {
  // TODO: nối contract thật P24 Thông báo
  listNotifications: (params?: { unreadOnly?: boolean; page?: number; pageSize?: number }) =>
    patientGet<{ items: PatientNotificationItem[]; unreadCount: number; hasMore: boolean }>(
      '/api/notifications/patient/notifications',
      { params },
    ),

  markNotificationRead: (notificationId: string) =>
    patientPost<void, Record<string, never>>(
      `/api/notifications/patient/notifications/${notificationId}/read`,
      {},
    ),

  // P25 Chatbot (`docs/api/chatbot.md`)
  checkChatHealth: () => patientGet<ChatHealthStatus>('/api/notifications/patient/chat/health'),

  getOrCreateSession: () => patientPost<ChatSessionResponse, Record<string, never>>('/api/notifications/patient/chat/sessions', {}),

  sendMessage: (sessionId: string, content: string, idempotencyKey: string) =>
    patientPost<ChatMessageDto, { content: string }>(
      `/api/notifications/patient/chat/sessions/${sessionId}/messages`,
      { content },
      { idempotencyKey },
    ),

  confirmProposal: (proposalId: string, patientProfileId: string, idempotencyKey: string) =>
    patientPost<{ bookingAttemptId: string }, { patientProfileId: string }>(
      `/api/scheduling/patient/proposals/${proposalId}/confirm`,
      { patientProfileId },
      { idempotencyKey },
    ),

  declineProposal: (proposalId: string) =>
    patientPost<void, Record<string, never>>(`/api/scheduling/patient/proposals/${proposalId}/decline`, {}),
};
