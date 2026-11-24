import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, FileEdit, FileText } from 'lucide-react';
import { clinicalStaffApi } from '../../api/clinicalStaffApi';
import {
  Button,
  Card,
  DataTable,
  DateTimeText,
  EmptyState,
  ErrorState,
  FormField,
  Input,
  Select,
  Skeleton,
  StatusPill,
} from '../../shared/ui';
import { extractFriendlyError } from '../../shared/lib/errors';
import { ClsRoomQueueItem } from '../../types/staffWorkflow';

/**
 * Danh sách các kết quả cận lâm sàng đã được chẩn đoán (RoomAccount).
 * Hiển thị các ca đã hoàn tất kỹ thuật, đang có kết quả nháp hoặc đã phát hành chính thức.
 */
export const RoomResultsPage: React.FC = () => {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'Published' | 'Draft'>('ALL');

  const query = useQuery({
    queryKey: ['staff', 'room-diagnosed-results'],
    queryFn: () => clinicalStaffApi.listDiagnosedResults(),
  });

  const allItems: ClsRoomQueueItem[] = query.data ?? [];

  // Lọc theo từ khóa tìm kiếm và trạng thái kết quả
  const filtered = allItems.filter((item) => {
    const term = keyword.trim().toLowerCase();
    const matchesKeyword =
      !term ||
      item.ticketNumber.toLowerCase().includes(term) ||
      item.patientName.toLowerCase().includes(term) ||
      item.serviceName.toLowerCase().includes(term) ||
      (item.readingDoctorName && item.readingDoctorName.toLowerCase().includes(term));

    const matchesStatus =
      statusFilter === 'ALL' || item.resultStatus === statusFilter;

    return matchesKeyword && matchesStatus;
  });

  const totalCount = allItems.length;
  const publishedCount = allItems.filter((r) => r.resultStatus === 'Published').length;
  const draftCount = allItems.filter((r) => r.resultStatus === 'Draft').length;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-headline-md text-2xl font-extrabold tracking-tight text-on-surface">
            Danh sách kết quả đã chẩn đoán
          </h1>
          <p className="text-sm text-on-surface-variant">
            Theo dõi, tra cứu các ca cận lâm sàng đã thực hiện kỹ thuật và có kết quả chẩn đoán.
          </p>
        </div>
        <Link
          to="/room"
          className="inline-flex items-center gap-1.5 rounded-xl bg-surface-container-low px-3.5 py-2 text-sm font-semibold text-primary transition-colors hover:bg-surface-container-high"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          Về hàng đợi CLS
        </Link>
      </div>

      {/* KPI Counters */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Card className="bg-surface-container-lowest">
          <span className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Tổng kết quả đã ghi nhận
          </span>
          <p className="font-data-tabular mt-1 text-2xl font-extrabold text-on-surface">{totalCount}</p>
        </Card>
        <Card className="bg-surface-container-lowest">
          <span className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Đã phát hành chính thức
          </span>
          <p className="font-data-tabular mt-1 text-2xl font-extrabold text-primary">{publishedCount}</p>
        </Card>
        <Card className="bg-surface-container-lowest">
          <span className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Bản nháp chờ phát hành
          </span>
          <p className="font-data-tabular mt-1 text-2xl font-extrabold text-[#D32F2F]">{draftCount}</p>
        </Card>
      </div>

      {/* Filter and Search */}
      <Card>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-12">
          <div className="sm:col-span-8">
            <FormField label="Tìm kiếm kết quả">
              {(props) => (
                <Input
                  {...props}
                  value={keyword}
                  onChange={(e) => setKeyword(e.target.value)}
                  placeholder="Tìm theo mã vé, tên bệnh nhân, tên dịch vụ CLS..."
                />
              )}
            </FormField>
          </div>
          <div className="sm:col-span-4">
            <FormField label="Lọc theo trạng thái kết quả">
              {(props) => (
                <Select
                  {...props}
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as 'ALL' | 'Published' | 'Draft')}
                  options={[
                    { value: 'ALL', label: 'Tất cả trạng thái' },
                    { value: 'Published', label: 'Đã phát hành (Published)' },
                    { value: 'Draft', label: 'Bản nháp (Draft)' },
                  ]}
                />
              )}
            </FormField>
          </div>
        </div>
      </Card>

      {/* Data Table */}
      {query.isLoading ? (
        <Card>
          <Skeleton rows={4} />
        </Card>
      ) : query.isError ? (
        <ErrorState
          message={extractFriendlyError(query.error).message}
          onRetry={() => query.refetch()}
        />
      ) : (
        <DataTable
          rowKey={(r) => r.orderItemId || r.queueTicketId || r.ticketNumber}
          rows={filtered}
          empty={
            <EmptyState
              title="Không tìm thấy kết quả chẩn đoán nào"
              description="Chưa có ca cận lâm sàng nào khớp với tiêu chí tìm kiếm."
            />
          }
          columns={[
            {
              key: 'ticket',
              header: 'Vé & Bệnh nhân',
              render: (r) => (
                <div>
                  <p className="font-mono font-bold text-[#006a41]">{r.ticketNumber}</p>
                  <p className="font-medium text-[#0f1f18]">{r.patientName || 'Bệnh nhân'}</p>
                  <p className="text-xs text-[#44544c]">
                    <DateTimeText value={r.serviceDate} mode="datetime" />
                  </p>
                </div>
              ),
            },
            {
              key: 'svc',
              header: 'Dịch vụ Cận lâm sàng',
              render: (r) => (
                <div>
                  <p className="font-semibold text-on-surface">{r.serviceName || 'Dịch vụ CLS'}</p>
                  {r.serviceCode && (
                    <span className="font-mono text-xs text-on-surface-variant">{r.serviceCode}</span>
                  )}
                </div>
              ),
            },
            {
              key: 'qStatus',
              header: 'Kỹ thuật',
              render: (r) => <StatusPill status={r.queueStatus} />,
            },
            {
              key: 'rStatus',
              header: 'Trạng thái kết quả',
              render: (r) => <StatusPill status={r.resultStatus} />,
            },
            {
              key: 'act',
              header: 'Thao tác',
              render: (r) => (
                <div className="flex flex-wrap items-center gap-2">
                  {r.resultStatus === 'Published' ? (
                    <Button
                      size="sm"
                      variant="outline"
                      leftIcon={<FileText className="h-3.5 w-3.5" />}
                      onClick={() => navigate(`/room/upload?orderItemId=${r.orderItemId}`)}
                    >
                      Chi tiết (Đã khóa)
                    </Button>
                  ) : (
                    <Button
                      size="sm"
                      variant="primary"
                      leftIcon={<FileEdit className="h-3.5 w-3.5" />}
                      onClick={() => navigate(`/room/upload?orderItemId=${r.orderItemId}`)}
                    >
                      Cập nhật / Trả KQ
                    </Button>
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
