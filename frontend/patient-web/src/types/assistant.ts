export interface PatientNotificationItem {
  id: string;
  title: string;
  body: string;
  category: 'Appointment' | 'Queue' | 'Payment' | 'Clinical' | 'System';
  status: 'Sent' | 'Read';
  createdAt: string;
  targetPath?: string;
}

/** Contract docs/api/chatbot.md §3 (API chat của .NET, E4). */
export interface ChatStatus {
  available: boolean;
}

export interface ChatConversation {
  conversationId: string;
  patientId: string;
  available: boolean;
}

/** Mã nút gợi ý (contract §6.2); nhãn hiển thị do web giữ. */
export type ChatSuggestionCode =
  | 'SEARCH_SLOTS'
  | 'VIEW_APPOINTMENTS'
  | 'VIEW_TICKET'
  | 'VIEW_EXAM_PAYMENT'
  | 'RESCHEDULE'
  | 'CANCEL'
  | 'SUGGEST_SPECIALTY';

/** Lựa chọn bấm được: bác sĩ (đặt lịch), lịch hẹn, chuyên khoa. Số suất trong nhãn chỉ để tham khảo. */
export interface ChatOption {
  kind: 'Doctor' | 'Appointment' | 'Specialty';
  label: string;
  specialtyCode?: string | null;
  doctorId?: string | null;
  serviceDate?: string | null;
  appointmentId?: string | null;
}

export interface ChatTurnReply {
  reply: string;
  suggestions: ChatSuggestionCode[];
  /** Đề nghị chờ xác nhận (contract §4, chờ H-06); hiện chatbot chưa tạo. */
  proposal: unknown | null;
  options: ChatOption[];
  /** Câu có dấu hiệu cấp cứu (chatbot gắn khi intent EMERGENCY): hiện nút gọi 115, không hiện nút đặt lịch. */
  emergency?: boolean;
}

/** Tin nhắn hiển thị trên khung chat (chỉ giữ trong trình duyệt). */
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  options?: ChatOption[];
  suggestions?: ChatSuggestionCode[];
  emergency?: boolean;
}

export interface DisplayQueueCard {
  roomCode: string;
  roomName: string;
  stationType: 'Reception' | 'Vitals' | 'Clinic' | 'Cls';
  currentTicketNumber: string | null;
  waitingTicketNumbers: string[];
  updatedAt: string;
}

export interface PublicDisplayBoardDto {
  hospitalName: string;
  generatedAt: string;
  rooms: DisplayQueueCard[];
}
