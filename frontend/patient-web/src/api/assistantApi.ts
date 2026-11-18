import { patientDelete, patientGet, patientPost } from '../shared/lib/http';
import type {
  ChatConversation,
  ChatStatus,
  ChatSuggestionCode,
  ChatTurnReply,
  PatientNotificationItem,
} from '../types/assistant';

const CHAT = '/api/notifications/patient/chat';

/** Đúng một trong text hoặc suggestion; patientId là hồ sơ đã chọn khi mở cuộc trò chuyện. */
export type ChatTurnBody = { patientId: string } & ({ text: string } | { suggestion: ChatSuggestionCode });

/**
 * Notifications & Chatbot Assistant API (Patient Web).
 * Chatbot theo `docs/api/chatbot.md` §3; thông báo còn `// TODO: nối contract thật`.
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

  // P25 Chatbot: API chat của .NET (contract §3). Web không gọi thẳng dịch vụ chatbot.
  getChatStatus: () => patientGet<ChatStatus>(`${CHAT}/status`),

  openConversation: (patientId: string) =>
    patientPost<ChatConversation, { patientId: string }>(`${CHAT}/conversations`, { patientId }),

  sendChatTurn: (conversationId: string, body: ChatTurnBody) =>
    patientPost<ChatTurnReply, ChatTurnBody>(`${CHAT}/conversations/${conversationId}/messages`, body),

  closeConversation: (conversationId: string) => patientDelete(`${CHAT}/conversations/${conversationId}`),
};
