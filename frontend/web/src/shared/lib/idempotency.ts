export function createIdempotencyKey(prefix?: string): string {
  const uuid =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
  // Backend lưu key tối đa 64 ký tự: giữ trọn uuid, cắt bớt tiền tố.
  return prefix ? `${prefix.slice(0, Math.max(0, 63 - uuid.length))}-${uuid}` : uuid;
}
