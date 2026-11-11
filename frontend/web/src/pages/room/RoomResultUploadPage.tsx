import React, { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Send, Upload } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
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

const MAX_CLS_FILE_BYTES = 20 * 1024 * 1024;
const ALLOWED_MIME_TYPES = new Set(['application/pdf', 'image/jpeg', 'image/png']);

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

  const uploadMutation = useMutation({
    mutationFn: (file: File) => clinicalStaffApi.uploadRoomDraftFile(orderItemId, file),
    onSuccess: () => {
      pushToast('Đã tải tệp nháp lên máy chủ an toàn.', 'success');
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

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (!ALLOWED_MIME_TYPES.has(file.type)) {
      pushToast('Chỉ chấp nhận định dạng PDF, JPG hoặc PNG.', 'danger');
      return;
    }
    if (file.size > MAX_CLS_FILE_BYTES) {
      pushToast('Dung lượng tệp vượt quá giới hạn tối đa 20MB.', 'danger');
      return;
    }
    uploadMutation.mutate(file);
  };

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
    <div className="space-y-4">
      <Link to="/room" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
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

            <div className="rounded-xl border border-dashed border-[#cddbd3] bg-[#f8fbf9] p-4">
              <label htmlFor="cls-draft-file" className="block text-sm font-medium text-[#0f1f18]">
                Đính kèm tệp kết quả nháp (PDF / JPG / PNG, tối đa 20MB)
              </label>
              <div className="mt-2 flex flex-wrap items-center gap-3">
                <input
                  id="cls-draft-file"
                  type="file"
                  accept=".pdf,.jpg,.jpeg,.png"
                  onChange={handleFileChange}
                  disabled={uploadMutation.isPending}
                />
                {uploadMutation.isPending ? (
                  <span className="inline-flex items-center gap-1 text-xs text-[#006a41]">
                    <Upload className="h-3.5 w-3.5" aria-hidden="true" />
                    Đang tải lên Backend...
                  </span>
                ) : null}
              </div>
              {d.draftFiles.length > 0 ? (
                <ul className="mt-3 space-y-1 text-xs text-[#0f1f18]">
                  {d.draftFiles.map((f) => (
                    <li key={f.fileId} className="font-mono">
                      • {f.fileName} ({Math.round(f.sizeBytes / 1024)} KB)
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>

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
