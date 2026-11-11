import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { CheckCircle2, Clock, FileUp, PhoneCall, Play } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Banner,
  Button,
  Card,
  DataTable,
  DateTimeText,
  EmptyState,
  ErrorState,
  Skeleton,
  StatusPill,
  useToast,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

/**
 * CL01–CL03 — Hàng đợi Phòng Cận lâm sàng (`RoomAccount`):
 * - Hỗ trợ đầy đủ luồng Waiting -> Called -> InProgress -> Completed ("Khám xong ≠ Có kết quả")
 * - Hiện ca ngày trước còn Pending kết quả để không bị sót việc phát hành kết quả.
 */
export const RoomPage: React.FC = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const query = useQuery({
    queryKey: ['staff', 'room-queue'],
    queryFn: () => clinicalStaffApi.listRoomQueue(),
  });

  const actionMutation = useMutation({
    mutationFn: ({
      orderItemId,
      action,
      version,
    }: {
      orderItemId: string;
      action: 'Call' | 'ConfirmPresence' | 'Defer' | 'Start' | 'FinishProcedure';
      version: number;
    }) => clinicalStaffApi.roomQueueAction(orderItemId, action, version),
    onSuccess: () => {
      pushToast('Đã cập nhật trạng thái thực hiện kỹ thuật.', 'success');
      queryClient.invalidateQueries({ queryKey: ['staff', 'room-queue'] });
    },
    onError: (err) => pushToast(extractFriendlyError(err).message, 'danger'),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Phòng Cận lâm sàng — Hàng đợi & Kết quả (CL01–CL03)</h1>
        <p className="text-xs text-[#44544c]">
          Hoàn tất thực hiện kỹ thuật là bước tách biệt với Phát hành kết quả chính thức; ca chưa phát hành từ ngày trước vẫn hiển thị tại đây.
        </p>
      </div>

      <Banner tone="info">
        Mỗi phòng CLS chỉ duy trì một phiên thiết bị đăng nhập. Nếu bị đăng nhập nơi khác, hệ thống sẽ tự dừng thao tác hiện tại.
      </Banner>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.orderItemId}
          rows={query.data ?? []}
          empty={<EmptyState title="Hàng đợi phòng CLS đang trống" />}
          columns={[
            {
              key: 'ticket',
              header: 'Vé & Bệnh nhân',
              render: (r) => (
                <div>
                  <p className="font-mono font-bold text-[#006a41]">{r.ticketNumber}</p>
                  <p className="font-medium text-[#0f1f18]">{r.patientName}</p>
                  <p className="text-xs text-[#44544c]"><DateTimeText value={r.serviceDate} mode="date" /></p>
                </div>
              ),
            },
            {
              key: 'svc',
              header: 'Dịch vụ CLS & BS đọc KQ',
              render: (r) => (
                <div>
                  <p className="font-semibold">{r.serviceName}</p>
                  <p className="text-xs text-[#44544c]">{r.serviceCode} • {r.readingDoctorName}</p>
                </div>
              ),
            },
            { key: 'qStatus', header: 'Trạng thái thực hiện', render: (r) => <StatusPill status={r.queueStatus} /> },
            { key: 'rStatus', header: 'Trạng thái kết quả', render: (r) => <StatusPill status={r.resultStatus} /> },
            {
              key: 'act',
              header: 'Điều phối & Kết quả',
              render: (r) => (
                <div className="flex flex-wrap gap-1.5">
                  {r.queueStatus === 'Waiting' ? (
                    <Button
                      size="sm"
                      variant="secondary"
                      leftIcon={<PhoneCall className="h-3.5 w-3.5" />}
                      onClick={() => actionMutation.mutate({ orderItemId: r.orderItemId, action: 'Call', version: r.version })}
                    >
                      Gọi lượt
                    </Button>
                  ) : null}
                  {r.queueStatus === 'Called' ? (
                    <>
                      <Button
                        size="sm"
                        variant="primary"
                        leftIcon={<Play className="h-3.5 w-3.5" />}
                        onClick={() => actionMutation.mutate({ orderItemId: r.orderItemId, action: 'ConfirmPresence', version: r.version })}
                      >
                        Bắt đầu làm
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        leftIcon={<Clock className="h-3.5 w-3.5" />}
                        onClick={() => actionMutation.mutate({ orderItemId: r.orderItemId, action: 'Defer', version: r.version })}
                      >
                        Hoãn lượt
                      </Button>
                    </>
                  ) : null}
                  {r.queueStatus === 'InProgress' ? (
                    <Button
                      size="sm"
                      variant="primary"
                      leftIcon={<CheckCircle2 className="h-3.5 w-3.5" />}
                      onClick={() => actionMutation.mutate({ orderItemId: r.orderItemId, action: 'FinishProcedure', version: r.version })}
                    >
                      Xong kỹ thuật
                    </Button>
                  ) : null}
                  {r.resultStatus !== 'Published' ? (
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<FileUp className="h-3.5 w-3.5" />}
                      onClick={() => navigate(`/room/upload?orderItemId=${r.orderItemId}`)}
                    >
                      Nhập / Upload KQ
                    </Button>
                  ) : (
                    <span className="text-xs font-medium text-[#44544c]">Đã phát hành (Khóa xem)</span>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}
    </div>
  );
};
