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
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-surface-container-low text-primary" aria-hidden="true">
          <Bell className="h-5 w-5" />
        </div>
        <div>
          <h1 className="font-headline-sm text-xl font-extrabold tracking-tight text-on-surface">Thông báo nội bộ (S03)</h1>
          <p className="text-xs text-on-surface-variant">Cảnh báo trễ check-in, thay đổi ca trực, duyệt hoàn tiền và phiên thiết bị.</p>
        </div>
      </div>

      {items.length === 0 ? (
        <EmptyState title="Không có thông báo mới" />
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <Card
              key={item.id}
              className={item.status === 'Sent' ? 'border-l-4 border-l-primary bg-surface-container-low/75' : ''}
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill status={item.status} />
                    <span className="text-xs text-on-surface-variant">
                      <DateTimeText value={item.createdAt} />
                    </span>
                  </div>
                  <h3 className="font-semibold text-on-surface">{item.title}</h3>
                  <p className="text-sm text-on-surface-variant">{item.body}</p>
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
