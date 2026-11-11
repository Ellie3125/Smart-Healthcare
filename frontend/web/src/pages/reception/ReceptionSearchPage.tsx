import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Search } from 'lucide-react';
import { receptionApi } from '../../api/receptionApi';
import {
  Button,
  Card,
  DataTable,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ReceptionSearchPage: React.FC = () => {
  const [keyword, setKeyword] = useState('');

  const query = useQuery({
    queryKey: ['reception', 'search', keyword],
    queryFn: () => receptionApi.searchUnified(keyword),
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-[#0f1f18]">Tìm kiếm hợp nhất Bệnh nhân & Lịch khám (RC01)</h1>
        <p className="text-xs text-[#44544c]">Tra cứu nhanh theo họ tên, số điện thoại hoặc mã lịch hẹn để điều phối check-in / thu ngân / hủy.</p>
      </div>

      <Card>
        <div className="flex items-end gap-3">
          <div className="flex-1">
            <FormField label="Từ khóa tra cứu">
              {(props) => (
                <Input
                  {...props}
                  value={keyword}
                  onChange={(e) => setKeyword(e.target.value)}
                  placeholder="Nhập tên, SĐT (0901...), hoặc mã lịch (SH-...)"
                />
              )}
            </FormField>
          </div>
          <Button variant="secondary" leftIcon={<Search className="h-4 w-4" />} onClick={() => query.refetch()}>
            Tìm kiếm
          </Button>
        </div>
      </Card>

      {query.isLoading ? (
        <Card><Skeleton rows={4} /></Card>
      ) : query.isError ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <DataTable
          rowKey={(r) => r.id}
          rows={query.data ?? []}
          empty={<EmptyState title="Không tìm thấy hồ sơ hoặc lịch khám phù hợp" />}
          columns={[
            {
              key: 'patient',
              header: 'Bệnh nhân',
              render: (r) => (
                <div>
                  <p className="font-semibold text-[#0f1f18]">{r.patientName}</p>
                  <p className="text-xs text-[#44544c] tabular-nums">
                    {r.phone} • CCCD {r.cccdMasked} • <StatusPill status={r.verificationStatus} />
                  </p>
                </div>
              ),
            },
            {
              key: 'apt',
              header: 'Lịch khám gần nhất',
              render: (r) =>
                r.appointmentCode ? (
                  <div>
                    <p className="font-mono text-xs font-semibold text-[#006a41]">{r.appointmentCode}</p>
                    <p className="text-xs text-[#44544c]">
                      {r.doctorName} • <DateTimeText value={r.serviceDate} mode="date" />
                    </p>
                  </div>
                ) : (
                  <span className="text-xs text-[#44544c]">Chưa có lịch hôm nay</span>
                ),
            },
            {
              key: 'status',
              header: 'Trạng thái',
              render: (r) => (r.appointmentStatus ? <StatusPill status={r.appointmentStatus} /> : '—'),
            },
            {
              key: 'act',
              header: 'Điều phối nhanh',
              render: (r) => (
                <div className="flex flex-wrap gap-2">
                  <Link to={r.appointmentCode ? `/reception/checkin?code=${encodeURIComponent(r.appointmentCode)}` : '/reception/checkin'}>
                    <Button size="sm" variant="primary">Check-in</Button>
                  </Link>
                  <Link to="/reception/cancel">
                    <Button size="sm" variant="outline">Hủy lịch</Button>
                  </Link>
                </div>
              ),
            },
          ]}
        />
      )}
    </div>
  );
};
