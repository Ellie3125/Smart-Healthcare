import React, { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Send } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import { ResultFileUploader } from '../../components/room/ResultFileUploader';
import {
  Banner,
  Button,
  Card,
  ConfirmDialog,
  ErrorState,
  FormField,
  Select,
  Skeleton,
  StatusPill,
  Textarea,
  useToast,
} from '../../shared/ui';
import { createIdempotencyKey } from '../../shared/lib/idempotency';
import { extractFriendlyError } from '../../shared/lib/errors';

/**
 * CL04 — Nhập kết quả CLS & Upload tệp Draft qua Backend (`PDF/JPG/PNG <= 20MB`):
 * - Tự chọn bác sĩ đọc kết quả từ ca trực phòng
 * - Sau khi `Published`, khóa xem/sửa tại phòng máy thực hiện.
 */
export const RoomResultUploadPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const orderItemId = searchParams.get('orderItemId') || 'cls-ord-1';
  const { pushToast } = useToast();

  const [summaryText, setSummaryText] = useState('');
  const [technicalConclusion, setTechnicalConclusion] = useState('');
  const [readingDoctorName, setReadingDoctorName] = useState('');
  const [publishOpen, setPublishOpen] = useState(false);

  const query = useQuery({
    queryKey: ['staff', 'room-draft', orderItemId],
    queryFn: () => clinicalStaffApi.getRoomResultDraft(orderItemId),
  });

  useEffect(() => {
    if (query.data) {
      setSummaryText(query.data.summaryText);
      setTechnicalConclusion(query.data.technicalConclusion);
      setReadingDoctorName(query.data.readingDoctorName);
    }
  }, [query.data]);

  const saveDraftMutation = useMutation({
    mutationFn: () =>
      clinicalStaffApi.saveRoomResultDraft(orderItemId, {
        summaryText,
        technicalConclusion,
        readingDoctorName,
        version: query.data!.version,
      }),
    onSuccess: () => {
      pushToast('Đã lưu bản nháp kết quả CLS.', 'success');
      query.refetch();
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  const publishMutation = useMutation({
    mutationFn: () =>
      clinicalStaffApi.publishRoomResult(
        orderItemId,
        query.data!.version,
        createIdempotencyKey(`room-pub-${orderItemId}`),
      ),
    onSuccess: () => {
      setPublishOpen(false);
      pushToast('Đã phát hành kết quả CLS. Quyền xem tại phòng đã được khóa.', 'success');
      query.refetch();
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  if (query.isLoading) {
    return (
      <Card title="Nhập kết quả Cận lâm sàng">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const d = query.data;
  const isPublished = d.resultStatus === 'Published';

  return (
    <div className="space-y-5">
      <Link to="/room" className="inline-flex items-center gap-1.5 rounded-xl bg-surface-container-low px-3.5 py-2 text-sm font-semibold text-primary transition-colors hover:bg-surface-container-high">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Quay lại hàng đợi phòng CLS
      </Link>

      <Card
        title={`${d.serviceName} (${d.serviceCode}) — Vé ${d.ticketNumber}`}
        subtitle={`Bệnh nhân: ${d.patientName}`}
        actions={<StatusPill status={d.resultStatus} />}
      >
        {isPublished ? (
          <Banner tone="warning" title="Kết quả đã phát hành — Đã khóa xem tại phòng thực hiện">
            Để bảo mật dữ liệu y tế, tài khoản phòng máy không thể xem lại hoặc sửa đổi tệp và nội dung sau khi đã phát hành chính thức.
          </Banner>
        ) : (
          <div className="space-y-4">
            <FormField label="Bác sĩ đọc & ký kết quả (theo ca trực phòng)" required>
              {(props) => (
                <Select
                  {...props}
                  value={readingDoctorName}
                  onChange={(e) => setReadingDoctorName(e.target.value)}
                  options={d.availableShiftDoctors.map((doc) => ({ value: doc, label: doc }))}
                />
              )}
            </FormField>

            <FormField label="Mô tả chi tiết kết quả">
              {(props) => <Textarea {...props} value={summaryText} onChange={(e) => setSummaryText(e.target.value)} />}
            </FormField>

            <FormField label="Kết luận kỹ thuật CLS">
              {(props) => (
                <Textarea
                  {...props}
                  value={technicalConclusion}
                  onChange={(e) => setTechnicalConclusion(e.target.value)}
                />
              )}
            </FormField>

            <ResultFileUploader
              queueTicketId={orderItemId}
              patientInfo={{
                fullName: d.patientName,
                ticketCode: d.ticketNumber,
              }}
              onUploadSuccess={() => query.refetch()}
              disabled={isPublished}
            />

            <div className="flex flex-wrap justify-end gap-2">
              <Button variant="outline" loading={saveDraftMutation.isPending} onClick={() => saveDraftMutation.mutate()}>
                Lưu nháp kết quả
              </Button>
              <Button
                variant="primary"
                leftIcon={<Send className="h-4 w-4" />}
                disabled={!summaryText.trim() || !readingDoctorName}
                onClick={() => setPublishOpen(true)}
              >
                Phát hành kết quả chính thức
              </Button>
            </div>
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={publishOpen}
        onClose={() => setPublishOpen(false)}
        onConfirm={() => publishMutation.mutate()}
        loading={publishMutation.isPending}
        title="Xác nhận phát hành kết quả CLS"
        message="Sau khi phát hành, bác sĩ lâm sàng sẽ nhận kết quả ngay và tài khoản phòng sẽ tự khóa quyền xem/sửa đối với ca này."
      />
    </div>
  );
};
