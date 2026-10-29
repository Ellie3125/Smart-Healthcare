# frontend-react

## Phạm vi
Antigravity: web React + Vite + TypeScript (frontend/**), app bệnh nhân React Native + Expo (mobile/**) và E2E.

## Khi nào đọc
Màn hình mới, sửa flow, hiển thị dữ liệu API hoặc accessibility.

## Quy tắc
- **FE-01** — UI tiếng Việt, responsive; mỗi request có loading/empty/error/validation/permission states. Chatbot ghi rõ chỉ hỗ trợ lịch, vé và thanh toán khám, không tư vấn y khoa; trạng thái thành công lấy từ API nghiệp vụ, không từ câu trả lời của model; khi chatbot chưa bật hoặc lỗi, giao diện thủ công vẫn đủ chức năng. Kết quả đã phát hành và chỉ định bác sĩ có nhãn phân biệt.
- **FE-02** — API client dùng contract version approved; xử lý 401/403/409/429 và timeout; không suy đoán enum/field. Form validation phục vụ UX, server vẫn kiểm nghiệp vụ.
- **FE-03** — Giữ state cục bộ tối thiểu, derive dữ liệu tính được; cleanup effect/abort request; cập nhật dựa state trước bằng functional update. Tối ưu sau khi đo, không thêm SWR/React Compiler chỉ vì skill nhắc.
- **FE-04** — Ưu tiên bàn phím, focus nhìn rõ, label/error liên kết input, contrast và reduced motion. Màu không là tín hiệu duy nhất. Không bắt người bệnh hiểu thuật ngữ hạ tầng.
- **FE-05** — Không lưu bệnh án/token dài hạn trong localStorage mặc định; chọn auth/cookie/CSRF theo ADR. Không coi bất kỳ VITE_* hoặc EXPO_PUBLIC_* nào là secret. Mock tách rõ khỏi API thật và không tự fallback sang mock khi production lỗi.
- **FE-06** — Web giữ React/Vite + React Hook Form; app bệnh nhân dùng React Native + Expo, push qua Expo Push/FCM do worker gửi; không dùng Next.js server actions/RSC/next/dynamic. UI dựa design tokens được thống nhất trong docs/ui khi Antigravity triển khai, không tự thêm font/CDN/analytics bên ngoài.

## Điều cấm
Không optimistic-confirm thanh toán/slot. Không sửa docs/api hay Backend. Không dùng phong cách quảng cáo lấn át thông tin y tế.

## Cách kiểm chứng
Component test tương tác; E2E với API thật cho happy/error/permission; viewport mobile/desktop, keyboard; dùng locator assertion chờ điều kiện, không bắt buộc networkidle hay sleep tùy ý.

## Nguồn
Giải pháp kỹ thuật v2.1 §8, §8.6; Master I §8, IV §3; React và Expo official docs; Playwright official API. frontend-design chỉ áp trong ranh giới y tế.
