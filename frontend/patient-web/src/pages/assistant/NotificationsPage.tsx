import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Bell, CheckCheck } from 'lucide-react';
import { assistantApi } from '../../api/assistantApi';
import {
  Button,
  Card,
  DateTimeText,
  EmptyState,
  ErrorState,
  Skeleton,
  StatusPill,
  Tabs,
} from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

export const NotificationsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<'all' | 'unread'>('all');

  const query = useQuery({
    queryKey: ['patient', 'notifications', filter],
    queryFn: () => assistantApi.listNotifications({ unreadOnly: filter === 'unread', page: 1, pageSize: 30 }),
  });

  const markReadMutation = useMutation({
    mutationFn: (id: string) => assistantApi.markNotificationRead(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['patient', 'notifications'] });
    },
  });

  const items = (query.data?.items ?? []).filter((n) => (filter === 'unread' ? n.status === 'Sent' : true));

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-2.5">
        <Bell className="h-6 w-6 text-[#006a41]" aria-hidden="true" />
        <div>
          <h1 className="text-2xl font-semibold text-[#0f1f18]">Thông báo của tôi</h1>
          <p className="text-sm text-[#3e4941]">Nhắc lịch khám, mời vào phòng, thanh toán CLS và cập nhật hoàn tiền.</p>
        </div>
      </div>

      <Tabs
        activeId={filter}
        onChange={(id) => setFilter(id as 'all' | 'unread')}
        tabs={[
          { id: 'all', label: 'Tất cả', count: query.data?.items.length ?? 0 },
          { id: 'unread', label: 'Chưa đọc', count: query.data?.unreadCount ?? 0 },
        ]}
      />

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState title="Không có thông báo mới" />
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <Card key={item.id} className={item.status === 'Sent' ? 'border-[#006a41]/50 bg-[#f8fbf9]' : ''}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill status={item.status} />
                    <span className="text-xs text-[#3e4941]">
                      <DateTimeText value={item.createdAt} />
                    </span>
                  </div>
                  <h3 className="font-semibold text-[#0f1f18]">{item.title}</h3>
                  <p className="text-sm text-[#3e4941]">{item.body}</p>
                </div>

                <div className="flex items-center gap-2">
                  {item.targetPath ? (
                    <Link to={item.targetPath}>
                      <Button size="sm" variant="secondary">
                        Mở ngay
                      </Button>
                    </Link>
                  ) : null}
                  {item.status === 'Sent' ? (
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<CheckCheck className="h-4 w-4" />}
                      loading={markReadMutation.isPending}
                      onClick={() => markReadMutation.mutate(item.id)}
                    >
                      Đánh dấu đã đọc
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
