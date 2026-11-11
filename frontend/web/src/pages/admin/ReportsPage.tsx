import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { adminOpsApi } from '../../api/adminOpsApi';
import {
  Button,
  Card,
  DataTable,
  DatePicker,
  ErrorState,
  FormField,
  MoneyText,
  Skeleton,
} from '../../shared/ui';
import { getVNDateISO } from '../../shared/lib/datetime';
import { downloadCsvFile } from '../../shared/lib/mask';
import { extractFriendlyError } from '../../shared/lib/errors';

export const ReportsPage: React.FC = () => {
  const [dateFrom, setDateFrom] = useState('2026-11-01');
  const [dateTo, setDateTo] = useState(getVNDateISO());

  const query = useQuery({
    queryKey: ['admin', 'reports', dateFrom, dateTo],
    queryFn: () => adminOpsApi.getReportSnapshot(dateFrom, dateTo),
  });

  const data = query.data;
  const maxRevenue = Math.max(1, ...(data?.dailySeries.map((d) => d.revenue) ?? [1]));

  const handleExportCsv = () => {
    if (!data) return;
    downloadCsvFile(
      `bao-cao-smart-healthcare-${data.dateFrom}-to-${data.dateTo}.csv`,
      ['Ngày', 'Số lịch đặt', 'Đã check-in', 'Doanh thu (VNĐ)'],
      data.dailySeries.map((r) => [r.date, r.bookings, r.checkedIn, r.revenue]),
    );
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-[#0f1f18]">Báo cáo vận hành & Doanh thu (AD14)</h1>
          <p className="text-xs text-[#44544c]">Tổng hợp từ bảng report_snapshots; hỗ trợ xuất CSV an toàn cho Excel.</p>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <FormField label="Từ ngày">
            {(props) => <DatePicker {...props} value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />}
          </FormField>
          <FormField label="Đến ngày">
            {(props) => <DatePicker {...props} value={dateTo} onChange={(e) => setDateTo(e.target.value)} />}
          </FormField>
          <Button variant="primary" leftIcon={<Download className="h-4 w-4" />} disabled={!data} onClick={handleExportCsv}>
            Xuất CSV
          </Button>
        </div>
      </div>

      {query.isLoading ? (
        <Card><Skeleton rows={5} /></Card>
      ) : query.isError || !data ? (
        <ErrorState message={extractFriendlyError(query.error).message} onRetry={() => query.refetch()} />
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <Card>
              <p className="text-xs text-[#44544c]">Tổng lịch khám</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-[#0f1f18]">{data.totalAppointments}</p>
            </Card>
            <Card>
              <p className="text-xs text-[#44544c]">Đã check-in</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-[#006a41]">{data.checkedInCount}</p>
            </Card>
            <Card>
              <p className="text-xs text-[#44544c]">Đã hủy</p>
              <p className="mt-1 text-2xl font-bold tabular-nums text-[#ba1a1a]">{data.cancelledCount}</p>
            </Card>
            <Card>
              <p className="text-xs text-[#44544c]">Tổng doanh thu</p>
              <p className="mt-1 text-xl font-bold text-[#006a41]"><MoneyText amount={data.totalRevenue} /></p>
            </Card>
            <Card>
              <p className="text-xs text-[#44544c]">Tổng đã hoàn</p>
              <p className="mt-1 text-xl font-bold text-[#9a5b00]"><MoneyText amount={data.totalRefunded} /></p>
            </Card>
          </div>

          <Card title="Biểu đồ doanh thu theo ngày">
            <div className="space-y-3">
              {data.dailySeries.map((d) => (
                <div key={d.date} className="space-y-1">
                  <div className="flex justify-between text-xs">
                    <span className="font-medium tabular-nums">{d.date}</span>
                    <MoneyText amount={d.revenue} />
                  </div>
                  <div className="h-3 w-full overflow-hidden rounded-full bg-[#e9f2ed]">
                    <div
                      className="h-full rounded-full bg-[#006a41]"
                      style={{ width: `${Math.round((d.revenue / maxRevenue) * 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </Card>

          <DataTable
            rowKey={(r) => r.date}
            rows={data.dailySeries}
            columns={[
              { key: 'date', header: 'Ngày', render: (r) => <span className="tabular-nums font-medium">{r.date}</span> },
              { key: 'bookings', header: 'Số lịch đặt', align: 'right', render: (r) => <span className="tabular-nums">{r.bookings}</span> },
              { key: 'checked', header: 'Đã check-in', align: 'right', render: (r) => <span className="tabular-nums">{r.checkedIn}</span> },
              { key: 'rev', header: 'Doanh thu', align: 'right', render: (r) => <MoneyText amount={r.revenue} /> },
            ]}
          />
        </>
      )}
    </div>
  );
};
