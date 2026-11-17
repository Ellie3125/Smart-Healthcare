export interface PatientNotificationItem {
  id: string;
  title: string;
  body: string;
  category: 'Appointment' | 'Queue' | 'Payment' | 'Clinical' | 'System';
  status: 'Sent' | 'Read';
  createdAt: string;
  targetPath?: string;
}

export interface ChatHealthStatus {
  available: boolean;
  message?: string;
}

export interface ChatProposalCard {
  proposalId: string;
  doctorId: string;
  doctorName: string;
  specialtyName: string;
  serviceDate: string;
  consultationFee: number;
  remainingSlots: number;
  status: 'Proposed' | 'Confirmed' | 'Declined' | 'Expired';
}

export interface ChatMessageDto {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  isRedFlagEmergency?: boolean;
  emergencyPhone?: string;
  suggestedSpecialty?: string;
  disclaimer?: string;
  proposal?: ChatProposalCard;
}

export interface ChatSessionResponse {
  sessionId: string;
  quickSuggestions: string[];
  messages: ChatMessageDto[];
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
