import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Bell, CheckCheck } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import { Button, Card, DateTimeText, EmptyState, ErrorState, Skeleton, StatusPill } from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const StaffNotificationsPage: React.FC = () => {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['staff', 'notifications'],
    queryFn: () => adminOpsApi.listNotifications(),
  });

  const readMutation = useMutation({
    mutationFn: (id: string) => adminOpsApi.markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff', 'notifications'] }),
  });

  if (query.isLoading) {
    return (
      <Card title="Thông báo điều phối & hệ thống">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError) {
    return <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const items = query.data?.items ?? [];

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2.5">
        <Bell className="h-6 w-6 text-[#006a41]" aria-hidden="true" />
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Thông báo nội bộ (S03)</h1>
          <p className="text-xs text-[#44544c]">Cảnh báo trễ check-in, thay đổi ca trực, duyệt hoàn tiền và phiên thiết bị.</p>
        </div>
      </div>

      {items.length === 0 ? (
        <EmptyState title="Không có thông báo mới" />
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <Card key={item.id}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill status={item.status} />
                    <span className="text-xs text-[#44544c]">
                      <DateTimeText value={item.createdAt} />
                    </span>
                  </div>
                  <h3 className="font-semibold text-[#0f1f18]">{item.title}</h3>
                  <p className="text-sm text-[#44544c]">{item.body}</p>
                </div>
                <div className="flex items-center gap-2">
                  {item.targetPath ? (
                    <Link to={item.targetPath}>
                      <Button size="sm" variant="secondary">
                        Đi tới xử lý
                      </Button>
                    </Link>
                  ) : null}
                  {item.status === 'Sent' ? (
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<CheckCheck className="h-4 w-4" />}
                      loading={readMutation.isPending}
                      onClick={() => readMutation.mutate(item.id)}
                    >
                      Đã đọc
                    </Button>
                  ) : null}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};
