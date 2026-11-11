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
    { label: 'Tổng đã trả', value: c.totalPaid, accent: 'text-[#0f1f18]' },
    { label: 'Tổng đã duyệt hoàn', value: c.totalApprovedRefund, accent: 'text-[#0b57d0]' },
    { label: 'Còn giữ ròng', value: c.netHeld, accent: 'text-[#006a41]' },
    { label: 'Miễn giảm', value: c.totalWaived, accent: 'text-[#9a5b00]' },
    { label: 'Hoàn thành công', value: c.totalCompletedRefund, accent: 'text-[#006a41]' },
  ];

  return (
    <div className="space-y-5">
      <Link to="/records" className="inline-flex items-center gap-1.5 text-sm font-medium text-[#006a41] hover:underline">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Về danh sách hồ sơ khám
      </Link>

      <div>
        <h1 className="text-2xl font-semibold text-[#0f1f18]">Chi phí đợt khám</h1>
        <p className="mt-1 text-sm text-[#3e4941]">
          {c.doctorName} • {c.specialtyName} • Ngày <DateTimeText value={c.serviceDate} mode="date" />
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        {metrics.map((m) => (
          <Card key={m.label}>
            <p className="text-xs text-[#3e4941]">{m.label}</p>
            <p className={`mt-1 text-xl font-bold ${m.accent}`}>
              <MoneyText amount={m.value} />
            </p>
          </Card>
        ))}
      </div>

      <Card title="Chi tiết các khoản phát sinh">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-[#e5f8ed] text-sm">
            <thead className="bg-[#e5f8ed]">
              <tr>
                <th className="px-4 py-2.5 text-left font-semibold text-[#0f1f18]">Nhóm</th>
                <th className="px-4 py-2.5 text-left font-semibold text-[#0f1f18]">Nội dung</th>
                <th className="px-4 py-2.5 text-right font-semibold text-[#0f1f18]">Đơn giá</th>
                <th className="px-4 py-2.5 text-right font-semibold text-[#0f1f18]">Miễn giảm</th>
                <th className="px-4 py-2.5 text-right font-semibold text-[#0f1f18]">Thực trả</th>
                <th className="px-4 py-2.5 text-center font-semibold text-[#0f1f18]">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#e5f8ed]">
              {c.lineItems.map((line) => (
                <tr key={line.id}>
                  <td className="px-4 py-3 text-[#3e4941]">{line.category}</td>
                  <td className="px-4 py-3 font-medium text-[#0f1f18]">{line.description}</td>
                  <td className="px-4 py-3 text-right">
                    <MoneyText amount={line.grossAmount} />
                  </td>
                  <td className="px-4 py-3 text-right text-[#9a5b00]">
                    <MoneyText amount={line.waiverAmount} />
                  </td>
                  <td className="px-4 py-3 text-right font-semibold text-[#006a41]">
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
