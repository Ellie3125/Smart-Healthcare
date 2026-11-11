import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminOpsApi } from '../../api/adminOpsApi';
import { Button, Card, ErrorState, FormField, Skeleton, StatusPill, Textarea, useToast } from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';
import type { ChatbotSpecialtyConfigItem } from '../../types/staffWorkflow';

export const ChatbotConfigPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const query = useQuery({
    queryKey: ['admin', 'chatbot-config'],
    queryFn: () => adminOpsApi.listChatbotConfigs(),
  });

  const saveMutation = useMutation({
    mutationFn: (cfg: ChatbotSpecialtyConfigItem) => adminOpsApi.saveChatbotConfig(cfg),
    onSuccess: () => {
      pushToast('Đã lưu cấu hình chuyên khoa chatbot.', 'success');
      queryClient.invalidateQueries({ queryKey: ['admin', 'chatbot-config'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Cấu hình Chuyên khoa & Red-flag Chatbot (AD17)</h1>
        <p className="text-xs text-[#44544c]">Quản lý từ khóa gợi ý chuyên khoa, từ khóa cảnh báo cấp cứu 115 và câu khuyến cáo chuẩn.</p>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <div className="space-y-4">
          {(query.data ?? []).map((cfg) => (
            <ChatbotConfigEditorCard key={cfg.id} item={cfg} onSave={(next) => saveMutation.mutate(next)} saving={saveMutation.isPending} />
          ))}
        </div>
      )}
    </div>
  );
};

const ChatbotConfigEditorCard: React.FC<{
  item: ChatbotSpecialtyConfigItem;
  onSave: (next: ChatbotSpecialtyConfigItem) => void;
  saving: boolean;
}> = ({ item, onSave, saving }) => {
  const [symptoms, setSymptoms] = React.useState(item.symptomKeywords.join(', '));
  const [redFlags, setRedFlags] = React.useState(item.redFlagKeywords.join(', '));
  const [disclaimer, setDisclaimer] = React.useState(item.disclaimerText);

  return (
    <Card
      title={`Chuyên khoa: ${item.specialtyName}`}
      actions={<StatusPill status={item.isEnabled ? 'Active' : 'Inactive'} />}
    >
      <div className="space-y-3">
        <FormField label="Từ khóa triệu chứng gợi ý (cách nhau dấu phẩy)">
          {(props) => <Textarea {...props} rows={2} value={symptoms} onChange={(e) => setSymptoms(e.target.value)} />}
        </FormField>
        <FormField label="Từ khóa cảnh báo đỏ 115 (Red-flag)">
          {(props) => <Textarea {...props} rows={2} value={redFlags} onChange={(e) => setRedFlags(e.target.value)} />}
        </FormField>
        <FormField label="Văn bản miễn trừ trách nhiệm (Disclaimer)">
          {(props) => <Textarea {...props} rows={2} value={disclaimer} onChange={(e) => setDisclaimer(e.target.value)} />}
        </FormField>
        <div className="flex justify-end">
          <Button
            variant="primary"
            loading={saving}
            onClick={() =>
              onSave({
                ...item,
                symptomKeywords: symptoms.split(',').map((s) => s.trim()).filter(Boolean),
                redFlagKeywords: redFlags.split(',').map((s) => s.trim()).filter(Boolean),
                disclaimerText: disclaimer,
              })
            }
          >
            Lưu cấu hình
          </Button>
        </div>
      </div>
    </Card>
  );
};
