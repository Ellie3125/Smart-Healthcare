import React, { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, Bot, PhoneCall, Send, Sparkles } from 'lucide-react';
import { assistantApi } from '../../api/assistantApi';
import { patientGet } from '../../shared/lib/http';
import {
  Banner,
  Button,
  Card,
  DateTimeText,
  FormField,
  Input,
  MoneyText,
  Select,
  Skeleton,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/money';
import { extractPatientError } from '../../shared/lib/errors';
import type { ChatMessageDto } from '../../types/assistant';

interface ProfileMini {
  id: string;
  fullName: string;
  relationship: string;
}

/**
 * P25 — Chatbot thông minh hỗ trợ điều hướng khám:
 * - Tôn trọng `/health` (`available=false` -> báo bảo trì)
 * - 3 nút gợi ý nhanh
 * - Thẻ gợi ý chuyên khoa có disclaimer
 * - Red-flag cảnh báo 115 TUYỆT ĐỐI KHÔNG render nút đặt lịch
 * - Thẻ Proposal xác nhận đặt lịch gọi API Backend
 */
export const ChatbotWorkspace: React.FC<{ compact?: boolean }> = ({ compact = false }) => {
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [inputText, setInputText] = useState('');
  const [localMessages, setLocalMessages] = useState<ChatMessageDto[] | null>(null);
  const [selectedProfileId, setSelectedProfileId] = useState('prof-self');

  const healthQuery = useQuery({
    queryKey: ['patient', 'chat-health'],
    queryFn: () => assistantApi.checkChatHealth(),
  });

  const sessionQuery = useQuery({
    queryKey: ['patient', 'chat-session'],
    queryFn: () => assistantApi.getOrCreateSession(),
    enabled: healthQuery.data?.available !== false,
  });

  const profilesQuery = useQuery({
    queryKey: ['patient', 'profiles'],
    queryFn: () => patientGet<ProfileMini[]>('/api/identity/patient/profiles'),
  });

  const isUnavailable = healthQuery.data?.available === false;
  const sessionId = sessionQuery.data?.sessionId ?? 'chat-session-1';
  const messages = localMessages ?? sessionQuery.data?.messages ?? [];
  const quickSuggestions = sessionQuery.data?.quickSuggestions ?? [];

  const sendMutation = useMutation({
    mutationFn: (content: string) =>
      assistantApi.sendMessage(sessionId, content, createIdempotencyKey('chat-msg')),
    onSuccess: (assistantReply, sentText) => {
      const userEntry: ChatMessageDto = {
        id: `local-u-${Date.now()}`,
        role: 'user',
        content: sentText,
        createdAt: new Date().toISOString(),
      };
      setLocalMessages((prev) => [...(prev ?? sessionQuery.data?.messages ?? []), userEntry, assistantReply]);
      setInputText('');
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  const confirmProposalMutation = useMutation({
    mutationFn: (proposalId: string) =>
      assistantApi.confirmProposal(
        proposalId,
        selectedProfileId,
        createIdempotencyKey(`prop-confirm-${proposalId}`),
      ),
    onSuccess: (res) => {
      pushToast('Đã tạo giữ chỗ 5 phút từ đề xuất của trợ lý.', 'success');
      navigate(`/booking/attempt/${res.bookingAttemptId}`);
    },
    onError: (err) => {
      pushToast(extractPatientError(err).message, 'danger');
    },
  });

  const declineProposalMutation = useMutation({
    mutationFn: (proposalId: string) => assistantApi.declineProposal(proposalId),
    onSuccess: () => {
      pushToast('Đã từ chối đề xuất đặt lịch này.', 'info');
    },
  });

  const handleSend = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || isUnavailable || sendMutation.isPending) return;
    sendMutation.mutate(trimmed);
  };

  return (
    <div className="flex flex-col gap-4">
      {isUnavailable ? (
        <Banner tone="warning" title="Trợ lý ảo tạm thời ngưng phục vụ">
          {healthQuery.data?.message ||
            'Dịch vụ tư vấn tự động đang được bảo trì. Bạn vẫn có thể đặt lịch trực tiếp qua danh sách bác sĩ.'}
        </Banner>
      ) : null}

      {sessionQuery.isLoading ? (
        <Skeleton rows={4} />
      ) : (
        <div
          className={`space-y-3 overflow-y-auto rounded-2xl border border-[#bdcabf] bg-[#f8fbf9] p-4 ${
            compact ? 'max-h-80' : 'min-h-[380px] max-h-[540px]'
          }`}
          aria-live="polite"
        >
          {messages.map((msg) => (
            <div
              key={msg.id}
              className={`flex flex-col ${msg.role === 'user' ? 'items-end' : 'items-start'}`}
            >
              <div
                className={`max-w-[85%] rounded-2xl px-4 py-3 text-sm ${
                  msg.role === 'user'
                    ? 'bg-[#006a41] text-white'
                    : msg.isRedFlagEmergency
                      ? 'border-2 border-[#b6171e] bg-[#ffdad6] text-[#93000a]'
                      : 'border border-[#bdcabf] bg-white text-[#0f1f18]'
                }`}
              >
                {msg.isRedFlagEmergency ? (
                  <div className="mb-2 flex items-center gap-2 font-bold text-[#93000a]">
                    <AlertTriangle className="h-5 w-5 shrink-0" aria-hidden="true" />
                    <span>CẢNH BÁO CẤP CỨU — GỌI NGAY {msg.emergencyPhone || '115'}</span>
                  </div>
                ) : null}

                <p className="whitespace-pre-wrap">{msg.content}</p>

                {msg.isRedFlagEmergency ? (
                  <div className="mt-3">
                    <a
                      href={`tel:${msg.emergencyPhone || '115'}`}
                      className="inline-flex items-center gap-2 rounded-xl bg-[#b6171e] px-4 py-2 text-xs font-semibold text-white"
                    >
                      <PhoneCall className="h-4 w-4" aria-hidden="true" />
                      Gọi cấp cứu {msg.emergencyPhone || '115'} ngay
                    </a>
                  </div>
                ) : null}

                {msg.suggestedSpecialty && !msg.isRedFlagEmergency ? (
                  <div className="mt-2 rounded-xl bg-[#e5f8ed] px-3 py-2 text-xs text-[#005231]">
                    Chuyên khoa gợi ý: <strong>{msg.suggestedSpecialty}</strong>
                  </div>
                ) : null}

                {msg.disclaimer ? (
                  <p className="mt-2 border-t border-black/10 pt-1.5 text-[11px] opacity-80">{msg.disclaimer}</p>
                ) : null}

                {msg.proposal && !msg.isRedFlagEmergency ? (
                  <div className="mt-3 space-y-2 rounded-xl border border-[#8ef8bb] bg-[#ebfef2] p-3 text-xs text-[#0f1f18]">
                    <p className="font-semibold text-[#005231]">Đề xuất đặt lịch khám</p>
                    <p>
                      {msg.proposal.doctorName} ({msg.proposal.specialtyName})
                    </p>
                    <p>
                      Ngày <DateTimeText value={msg.proposal.serviceDate} mode="date" /> • Phí:{' '}
                      <MoneyText amount={msg.proposal.consultationFee} /> • Còn {msg.proposal.remainingSlots} suất
                    </p>

                    {(profilesQuery.data ?? []).length > 0 ? (
                      <FormField label="Hồ sơ áp dụng">
                        {(props) => (
                          <Select
                            {...props}
                            value={selectedProfileId}
                            onChange={(e) => setSelectedProfileId(e.target.value)}
                            options={(profilesQuery.data ?? []).map((p) => ({
                              value: p.id,
                              label: `${p.fullName} (${p.relationship})`,
                            }))}
                          />
                        )}
                      </FormField>
                    ) : null}

                    <div className="flex flex-wrap gap-2 pt-1">
                      <Button
                        size="sm"
                        variant="primary"
                        loading={confirmProposalMutation.isPending}
                        onClick={() => confirmProposalMutation.mutate(msg.proposal!.proposalId)}
                      >
                        Xác nhận đặt lịch
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        loading={declineProposalMutation.isPending}
                        onClick={() => declineProposalMutation.mutate(msg.proposal!.proposalId)}
                      >
                        Từ chối
                      </Button>
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          ))}
        </div>
      )}

      {!isUnavailable && quickSuggestions.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {quickSuggestions.slice(0, 3).map((sug) => (
            <button
              key={sug}
              type="button"
              onClick={() => handleSend(sug)}
              className="inline-flex items-center gap-1.5 rounded-full border border-[#8ef8bb] bg-[#e5f8ed] px-3 py-1.5 text-xs font-medium text-[#005231] hover:bg-[#d4f7e2]"
            >
              <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
              {sug}
            </button>
          ))}
        </div>
      ) : null}

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          handleSend(inputText);
        }}
      >
        <label htmlFor={compact ? 'chat-input-widget' : 'chat-input-page'} className="sr-only">
          Nhập triệu chứng hoặc câu hỏi
        </label>
        <Input
          id={compact ? 'chat-input-widget' : 'chat-input-page'}
          disabled={isUnavailable || sendMutation.isPending}
          value={inputText}
          onChange={(e) => setInputText(e.target.value)}
          placeholder={isUnavailable ? 'Trợ lý đang bảo trì...' : 'Mô tả triệu chứng hoặc hỏi lịch khám...'}
        />
        <Button
          type="submit"
          variant="primary"
          disabled={isUnavailable || !inputText.trim()}
          loading={sendMutation.isPending}
          leftIcon={<Send className="h-4 w-4" />}
        >
          Gửi
        </Button>
      </form>
    </div>
  );
};

export const ChatbotPage: React.FC = () => (
  <div className="mx-auto max-w-4xl space-y-5">
    <div className="flex items-center gap-2.5">
      <Bot className="h-6 w-6 text-[#006a41]" aria-hidden="true" />
      <div>
        <h1 className="text-2xl font-semibold text-[#0f1f18]">Trợ lý hỗ trợ khám bệnh</h1>
        <p className="text-sm text-[#3e4941]">Hỏi gợi ý chuyên khoa, tra quy trình hoặc nhận đề xuất giữ chỗ nhanh.</p>
      </div>
    </div>
    <Card>
      <ChatbotWorkspace />
    </Card>
  </div>
);
