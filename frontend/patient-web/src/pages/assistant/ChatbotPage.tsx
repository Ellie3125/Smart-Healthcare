import React, { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { AlertTriangle, Bot, CalendarPlus, PhoneCall, RotateCcw, Send, Sparkles } from 'lucide-react';
import { assistantApi, type ChatTurnBody } from '../../api/assistantApi';
import { patientGet } from '../../shared/lib/http';
import { Banner, Button, Card, FormField, Input, Select, Skeleton, useToast } from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';
import type { ChatMessage, ChatOption, ChatSuggestionCode, ChatTurnReply } from '../../types/assistant';

interface ProfileMini {
  patientId: string;
  fullName: string;
  relationship: string;
}

/** Nhãn nút gợi ý; mã trùng intent của chatbot (contract §6.2). */
const SUGGESTION_LABELS: Record<ChatSuggestionCode, string> = {
  SEARCH_SLOTS: 'Tìm lịch khám',
  VIEW_APPOINTMENTS: 'Xem lịch của tôi',
  VIEW_TICKET: 'Xem vé',
  VIEW_EXAM_PAYMENT: 'Thanh toán khám',
  RESCHEDULE: 'Đổi lịch',
  CANCEL: 'Hủy lịch',
  SUGGEST_SPECIALTY: 'Tôi nên khám khoa nào',
};
const ALL_SUGGESTIONS = Object.keys(SUGGESTION_LABELS) as ChatSuggestionCode[];
const TEXT_MAX_LENGTH = 1000;

const WELCOME =
  'Xin chào! Mình là trợ lý Smart Healthcare. Mình giúp bạn tìm lịch khám, xem lịch hẹn, vé, phí khám và gợi ý chuyên khoa. ' +
  'Mình không chẩn đoán bệnh; đặt, đổi, hủy lịch bạn tự xác nhận trên web.';
const UNAVAILABLE_REPLY =
  'Trợ lý đang tạm ngưng. Bạn vẫn có thể đặt lịch và tra cứu trực tiếp trên web, hoặc thử lại sau ít phút.';

/** API chat chỉ trả mã lỗi (title = code); câu hiển thị do web giữ. */
const CHAT_ERROR_MESSAGES: Record<string, string> = {
  TOOMANYREQUESTS: 'Bạn gửi tin nhắn quá nhanh. Vui lòng thử lại sau ít phút.',
  PROFILENOTFOUND: 'Không tìm thấy hồ sơ bệnh nhân hoặc bạn không còn quyền đặt lịch cho hồ sơ này.',
  INVALIDINPUT: 'Tin nhắn không hợp lệ (tối đa 1000 ký tự).',
  SESSIONEXPIRED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
};
const chatErrorMessage = (err: unknown) => {
  const error = extractPatientError(err);
  return { code: error.code, message: CHAT_ERROR_MESSAGES[error.code] ?? error.message };
};

let messageSeq = 0;
const nextId = (prefix: string) => `${prefix}-${Date.now()}-${++messageSeq}`;
const welcomeMessage = (): ChatMessage => ({ id: nextId('welcome'), role: 'assistant', content: WELCOME });

/**
 * P25 — Trợ lý trên web bệnh nhân (contract docs/api/chatbot.md §3, §6):
 * - Gọi API chat của .NET; mỗi cuộc trò chuyện gắn với một hồ sơ, đổi hồ sơ là mở cuộc mới
 * - `status.available=false` -> báo tạm ngưng, khóa ô nhập; nút gợi ý vẫn bấm được (đường tắt không cần model)
 * - Lựa chọn bác sĩ mở trang đặt lịch; lịch hẹn mở vé, đổi, hủy; chuyên khoa hỏi tiếp lịch khám
 * - Câu có dấu hiệu cấp cứu: hiện nút gọi 115, TUYỆT ĐỐI KHÔNG hiện nút đặt lịch
 */
export const ChatbotWorkspace: React.FC<{ compact?: boolean }> = ({ compact = false }) => {
  const { pushToast } = useToast();
  const [inputText, setInputText] = useState('');
  const [selectedProfileId, setSelectedProfileId] = useState('');
  const [generation, setGeneration] = useState(0);
  const [messages, setMessages] = useState<ChatMessage[]>(() => [welcomeMessage()]);
  const scrollRef = useRef<HTMLDivElement>(null);

  const statusQuery = useQuery({
    queryKey: ['patient', 'chat-status'],
    queryFn: () => assistantApi.getChatStatus(),
    refetchInterval: 30_000,
  });

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<ProfileMini[]>('/api/patient/profiles'),
  });
  const profiles = profilesQuery.data ?? [];
  const activeProfileId = selectedProfileId || profiles[0]?.patientId || '';

  const conversationQuery = useQuery({
    queryKey: ['patient', 'chat-conversation', activeProfileId, generation],
    queryFn: () => assistantApi.openConversation(activeProfileId),
    enabled: Boolean(activeProfileId),
    staleTime: Infinity,
    gcTime: 0,
    retry: false,
  });
  const conversationId = conversationQuery.data?.conversationId;

  // Cuộc trò chuyện mới (đổi hồ sơ hoặc bấm "Cuộc mới"): bắt đầu lại khung chat.
  useEffect(() => {
    if (conversationId) setMessages([welcomeMessage()]);
  }, [conversationId]);

  useEffect(() => {
    const box = scrollRef.current;
    if (box) box.scrollTop = box.scrollHeight;
  }, [messages]);

  const isUnavailable = statusQuery.data?.available === false;

  const startNewConversation = () => {
    if (conversationId) void assistantApi.closeConversation(conversationId).catch(() => undefined);
    setGeneration((g) => g + 1);
  };

  const sendMutation = useMutation({
    mutationFn: ({ body }: { body: ChatTurnBody; display: string }) =>
      assistantApi.sendChatTurn(conversationId!, body),
    onMutate: ({ display }) => {
      setMessages((prev) => [...prev, { id: nextId('u'), role: 'user', content: display }]);
    },
    onSuccess: (reply: ChatTurnReply) => {
      setMessages((prev) => [
        ...prev,
        {
          id: nextId('a'),
          role: 'assistant',
          content: reply.reply,
          options: reply.options,
          suggestions: reply.suggestions,
          emergency: reply.emergency === true,
        },
      ]);
    },
    onError: (err) => {
      const error = chatErrorMessage(err);
      if (error.code === 'CHATBOTUNAVAILABLE') {
        setMessages((prev) => [...prev, { id: nextId('a'), role: 'assistant', content: UNAVAILABLE_REPLY }]);
        void statusQuery.refetch();
        return;
      }
      if (error.code === 'CONVERSATIONNOTFOUND') {
        pushToast('Cuộc trò chuyện đã hết hạn, đã mở cuộc mới. Bạn gửi lại giúp mình nhé.', 'info');
        setGeneration((g) => g + 1);
        return;
      }
      if (error.code === 'PROFILENOTFOUND') void profilesQuery.refetch();
      pushToast(error.message, 'danger');
    },
  });

  const canSend = Boolean(conversationId) && !sendMutation.isPending;

  // typed=false: câu do web tạo khi bấm lựa chọn chuyên khoa, đi đường tắt nên gửi được cả khi model tạm ngưng.
  const sendText = (text: string, typed = true) => {
    const trimmed = text.trim().slice(0, TEXT_MAX_LENGTH);
    if (!trimmed || !canSend || (typed && isUnavailable)) return;
    sendMutation.mutate({ body: { patientId: activeProfileId, text: trimmed }, display: trimmed });
    setInputText('');
  };

  const sendSuggestion = (code: ChatSuggestionCode) => {
    if (!canSend) return;
    sendMutation.mutate({ body: { patientId: activeProfileId, suggestion: code }, display: SUGGESTION_LABELS[code] });
  };

  const lastAssistant = [...messages].reverse().find((m) => m.role === 'assistant');
  const chips = lastAssistant?.suggestions?.length ? lastAssistant.suggestions : ALL_SUGGESTIONS;

  if (profilesQuery.isSuccess && profiles.length === 0) {
    return (
      <Banner tone="info" title="Chưa có hồ sơ bệnh nhân">
        Bạn cần tạo hồ sơ trước khi dùng trợ lý. <Link to="/profiles/new" className="font-semibold underline">Tạo hồ sơ</Link>
      </Banner>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {isUnavailable ? (
        <Banner tone="warning" title="Trợ lý ảo tạm thời ngưng phục vụ">
          Trợ lý chỉ trả lời các nút gợi ý bên dưới. Bạn vẫn có thể đặt lịch trực tiếp qua danh sách bác sĩ.
        </Banner>
      ) : null}

      <div className="flex flex-wrap items-end gap-2">
        {profiles.length > 1 ? (
          <div className="min-w-[200px] flex-1">
            <FormField label="Hỏi cho hồ sơ">
              {(props) => (
                <Select
                  {...props}
                  value={activeProfileId}
                  onChange={(e) => setSelectedProfileId(e.target.value)}
                  options={profiles.map((p) => ({ value: p.patientId, label: `${p.fullName} (${p.relationship})` }))}
                />
              )}
            </FormField>
          </div>
        ) : null}
        <Button
          size="sm"
          variant="outline"
          leftIcon={<RotateCcw className="h-4 w-4" />}
          onClick={startNewConversation}
          disabled={!conversationId || sendMutation.isPending}
        >
          Cuộc mới
        </Button>
      </div>

      {conversationQuery.isError ? (
        <Banner tone="danger" title="Không mở được cuộc trò chuyện">
          {chatErrorMessage(conversationQuery.error).message}
        </Banner>
      ) : null}

      {conversationQuery.isLoading || profilesQuery.isLoading ? (
        <Skeleton rows={4} />
      ) : (
        <div
          ref={scrollRef}
          className={`space-y-3 overflow-y-auto rounded-2xl bg-surface-container-low/50 p-4 ${
            compact ? 'max-h-80' : 'min-h-[380px] max-h-[540px]'
          }`}
          aria-live="polite"
        >
          {messages.map((msg) => (
            <MessageBubble key={msg.id} message={msg} onPickSpecialty={(text) => sendText(text, false)} disabled={!canSend} />
          ))}
          {sendMutation.isPending ? (
            <p className="text-xs text-on-surface-variant" role="status">
              Trợ lý đang trả lời...
            </p>
          ) : null}
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        {chips.map((code) => (
          <button
            key={code}
            type="button"
            disabled={!canSend}
            onClick={() => sendSuggestion(code)}
            className="inline-flex items-center gap-1.5 rounded-full bg-surface-container-lowest px-3.5 py-1.5 text-xs font-medium text-on-surface shadow-sm ring-1 ring-outline-variant/25 transition-colors hover:bg-surface-container-low hover:text-primary disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
            {SUGGESTION_LABELS[code] ?? code}
          </button>
        ))}
      </div>

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          sendText(inputText);
        }}
      >
        <label htmlFor={compact ? 'chat-input-widget' : 'chat-input-page'} className="sr-only">
          Nhập triệu chứng hoặc câu hỏi
        </label>
        <Input
          id={compact ? 'chat-input-widget' : 'chat-input-page'}
          disabled={isUnavailable || !conversationId}
          value={inputText}
          maxLength={TEXT_MAX_LENGTH}
          onChange={(e) => setInputText(e.target.value)}
          placeholder={isUnavailable ? 'Trợ lý đang bảo trì...' : 'Mô tả triệu chứng hoặc hỏi lịch khám...'}
        />
        <Button
          type="submit"
          variant="primary"
          disabled={isUnavailable || !inputText.trim() || !conversationId}
          loading={sendMutation.isPending}
          leftIcon={<Send className="h-4 w-4" />}
        >
          Gửi
        </Button>
      </form>
    </div>
  );
};

const MessageBubble: React.FC<{
  message: ChatMessage;
  onPickSpecialty: (text: string) => void;
  disabled: boolean;
}> = ({ message, onPickSpecialty, disabled }) => {
  const isUser = message.role === 'user';
  return (
    <div className={`flex flex-col ${isUser ? 'items-end' : 'items-start'}`}>
      <div
        className={`max-w-[85%] rounded-2xl px-4 py-3 text-sm shadow-sm ${
          isUser
            ? 'rounded-tr-sm bg-primary text-white'
            : message.emergency
              ? 'rounded-tl-sm border-2 border-error bg-error-container text-[#93000a]'
              : 'rounded-tl-sm bg-surface-container-lowest text-on-surface'
        }`}
      >
        {message.emergency ? (
          <div className="mb-2 flex items-center gap-2 font-bold text-[#93000a]">
            <AlertTriangle className="h-5 w-5 shrink-0" aria-hidden="true" />
            <span>CẢNH BÁO CẤP CỨU — GỌI NGAY 115</span>
          </div>
        ) : null}

        <p className="whitespace-pre-wrap">{message.content}</p>

        {message.emergency ? (
          <div className="mt-3">
            <a
              href="tel:115"
              className="inline-flex items-center gap-2 rounded-xl bg-[#b6171e] px-4 py-2 text-xs font-semibold text-white"
            >
              <PhoneCall className="h-4 w-4" aria-hidden="true" />
              Gọi cấp cứu 115 ngay
            </a>
          </div>
        ) : message.options?.length ? (
          <ul className="mt-3 space-y-2">
            {message.options.map((option, i) => (
              <li key={`${option.kind}-${i}`}>
                <OptionCard option={option} onPickSpecialty={onPickSpecialty} disabled={disabled} />
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </div>
  );
};

const linkClass =
  'inline-flex items-center gap-1 rounded-lg bg-primary px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#005231]';
const ghostLinkClass =
  'inline-flex items-center rounded-lg px-2.5 py-1 text-[11px] font-semibold text-primary ring-1 ring-primary/30 hover:bg-primary-container';

/** Lựa chọn trên khung chat. Đặt, đổi, hủy đều làm trên trang thủ công; chatbot không tự thay đổi dữ liệu (BOT-05). */
const OptionCard: React.FC<{ option: ChatOption; onPickSpecialty: (text: string) => void; disabled: boolean }> = ({
  option,
  onPickSpecialty,
  disabled,
}) => {
  if (option.kind === 'Doctor' && option.doctorId) {
    return (
      <div className="rounded-xl bg-surface-container-low/80 p-3 text-xs">
        <p>{option.label}</p>
        <Link
          to={`/booking/doctors/${option.doctorId}${option.serviceDate ? `?date=${option.serviceDate}` : ''}`}
          className={`mt-2 ${linkClass}`}
        >
          <CalendarPlus className="h-3.5 w-3.5" aria-hidden="true" />
          Đặt lịch
        </Link>
      </div>
    );
  }
  if (option.kind === 'Appointment' && option.appointmentId) {
    return (
      <div className="rounded-xl bg-surface-container-low/80 p-3 text-xs">
        <p>{option.label}</p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <Link to={`/appointments/${option.appointmentId}/ticket`} className={linkClass}>
            Xem vé
          </Link>
          <Link to={`/appointments/${option.appointmentId}/reschedule`} className={ghostLinkClass}>
            Đổi lịch
          </Link>
          <Link to={`/appointments/${option.appointmentId}/cancel`} className={ghostLinkClass}>
            Hủy lịch
          </Link>
        </div>
      </div>
    );
  }
  if (option.kind === 'Specialty') {
    const name = option.label.split(':')[0].trim();
    return (
      <button
        type="button"
        disabled={disabled}
        onClick={() => onPickSpecialty(`tôi muốn khám ${name.toLowerCase()}`)}
        className="w-full rounded-xl bg-surface-container-low/80 p-3 text-left text-xs transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-60"
      >
        {option.label}
      </button>
    );
  }
  return <p className="text-xs">{option.label}</p>;
};

export const ChatbotPage: React.FC = () => (
  <div className="mx-auto max-w-4xl space-y-6">
    <div className="flex items-center gap-3">
      <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-surface-container-low text-primary" aria-hidden="true">
        <Bot className="h-5 w-5" />
      </div>
      <div>
        <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">Trợ lý hỗ trợ khám bệnh</h1>
        <p className="text-sm text-on-surface-variant">Tìm lịch khám, xem lịch hẹn, vé, phí khám và gợi ý chuyên khoa.</p>
      </div>
    </div>
    <Card>
      <ChatbotWorkspace />
    </Card>
  </div>
);
