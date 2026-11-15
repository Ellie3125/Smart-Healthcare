import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { clinicalApi } from '../../api/clinicalApi';
import { Card, DateTimeText, ErrorState, MoneyText, Skeleton, StatusPill } from '../../shared/ui';
import { extractPatientError } from '../../shared/lib/errors';

/**
 * P18 — Tổng quan chi phí đợt khám với 5 chỉ số tài chính chuẩn.
 */
export const EncounterCostPage: React.FC = () => {
  const { encounterId = 'enc-hist-1' } = useParams<{ encounterId: string }>();

  const query = useQuery({
    queryKey: ['patient', 'encounter-cost', encounterId],
    queryFn: () => clinicalApi.getEncounterCostSummary(encounterId),
  });

  if (query.isLoading) {
    return (
      <Card title="Tổng quan chi phí đợt khám">
        <Skeleton rows={4} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return <ErrorState message={extractPatientError(query.error).message} onRetry={() => query.refetch()} />;
  }

  const c = query.data;
  const metrics = [
    { label: 'Tổng đã trả', value: c.totalPaid, accent: 'text-on-surface', bg: 'bg-surface-container-lowest' },
    { label: 'Tổng đã duyệt hoàn', value: c.totalApprovedRefund, accent: 'text-[#0b57d0]', bg: 'bg-surface-container-lowest' },
    { label: 'Còn giữ ròng', value: c.netHeld, accent: 'text-primary', bg: 'bg-secondary-container/45' },
    { label: 'Miễn giảm', value: c.totalWaived, accent: 'text-[#9a5b00]', bg: 'bg-surface-container-lowest' },
    { label: 'Hoàn thành công', value: c.totalCompletedRefund, accent: 'text-primary', bg: 'bg-surface-container-lowest' },
  ];

  return (
    <div className="space-y-6">
      <Link
        to="/records"
        className="inline-flex items-center gap-1.5 rounded-xl bg-surface-container-low px-3.5 py-2 text-sm font-semibold text-primary hover:bg-surface-container transition-colors"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Về danh sách hồ sơ khám
      </Link>

      <div>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container/75 px-3 py-0.5 text-xs font-semibold text-[#005231] mb-2">
          Minh bạch viện phí
        </span>
        <h1 className="text-2xl sm:text-3xl font-bold text-on-surface tracking-tight">Chi phí đợt khám</h1>
        <p className="mt-1 text-sm text-on-surface-variant">
          {c.doctorName} • {c.specialtyName} • Ngày <DateTimeText value={c.serviceDate} mode="date" />
        </p>
      </div>

      <div className="grid gap-3.5 sm:grid-cols-2 lg:grid-cols-5">
        {metrics.map((m) => (
          <div
            key={m.label}
            className={`rounded-2xl ${m.bg} border border-surface-container-highest/65 p-4 shadow-[0_1px_8px_rgba(15,31,24,0.04)]`}
          >
            <p className="text-xs font-medium text-on-surface-variant">{m.label}</p>
            <p className={`mt-1.5 text-xl font-bold tabular-nums ${m.accent}`}>
              <MoneyText amount={m.value} />
            </p>
          </div>
        ))}
      </div>

      <Card title="Chi tiết các khoản phát sinh">
        <div className="overflow-x-auto rounded-xl">
          <table className="min-w-full divide-y divide-surface-container-low text-sm">
            <thead className="bg-surface-container-low">
              <tr>
                <th className="px-4 py-3 text-left font-semibold text-on-surface">Nhóm</th>
                <th className="px-4 py-3 text-left font-semibold text-on-surface">Nội dung</th>
                <th className="px-4 py-3 text-right font-semibold text-on-surface">Đơn giá</th>
                <th className="px-4 py-3 text-right font-semibold text-on-surface">Miễn giảm</th>
                <th className="px-4 py-3 text-right font-semibold text-on-surface">Thực trả</th>
                <th className="px-4 py-3 text-center font-semibold text-on-surface">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-container-low bg-surface-container-lowest">
              {c.lineItems.map((line) => (
                <tr key={line.id} className="hover:bg-surface-container-low/40 transition-colors">
                  <td className="px-4 py-3 text-on-surface-variant">{line.category}</td>
                  <td className="px-4 py-3 font-medium text-on-surface">{line.description}</td>
                  <td className="px-4 py-3 text-right tabular-nums">
                    <MoneyText amount={line.grossAmount} />
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-[#9a5b00]">
                    <MoneyText amount={line.waiverAmount} />
                  </td>
                  <td className="px-4 py-3 text-right font-semibold tabular-nums text-primary">
                    <MoneyText amount={line.netAmount} />
                  </td>
                  <td className="px-4 py-3 text-center">
                    <StatusPill status={line.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
};
