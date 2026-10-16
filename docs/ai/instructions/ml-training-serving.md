# ml-training-serving

## Phạm vi
Codex: training offline và FastAPI nội bộ.

## Khi nào đọc
Chọn dataset, EDA, train/evaluate, artifact hoặc inference contract.

## Quy tắc
- **ML-01** — Chốt nguồn, license, nhãn và đặc trưng có sẵn trước khám; tách test trước preprocessing; split theo bệnh nhân/thời gian/nguồn nếu cần. Fit và chọn hyperparameter/ngưỡng chỉ trên train/validation. Không bịa metrics.
- **ML-02** — ML lỗi/timeout/abstain vẫn có luồng đặt lịch thủ công. Không trả mẫu hard-code để che lỗi model. Backend gọi service nội bộ với timeout có giới hạn.
- **ML-03** — Bài toán bắt buộc là gợi ý chuyên khoa: multiclass nếu mỗi mẫu một nhãn, multi-label chỉ khi dữ liệu thật hỗ trợ. Nhãn disease→specialty là suy dẫn và cần ghi giới hạn/thẩm định.
- **ML-04** — Baseline so với CatBoost hoặc LightGBM; báo macro-F1, từng lớp, confusion matrix, top-K phù hợp, latency p50/p95, abstention, phân tích lỗi. Không cam kết 95% hay suy hiệu quả lâm sàng từ dữ liệu tổng hợp.
- **ML-05** — Artifact gồm pipeline, feature/label schema, seed/version/checksum/metrics/model card. Nạp một lần lúc khởi động, liveness/readiness tách; response có request_id, model_version, schema_version. Không tự retrain khi request/deploy.
- **ML-06** — Không gửi CCCD, tên, thanh toán hoặc dữ liệu không cần thiết sang ML; service không sửa DB nghiệp vụ. Gợi ý cận lâm sàng chỉ mở sau data gate; chưa đủ dữ liệu thì tắt hoặc quy tắc được ghi rõ.

## Điều cấm
Không dùng xét nghiệm/chẩn đoán sau khám làm đầu vào dự đoán trước khám. Không tải pickle/artifact không tin cậy, không public route /ml qua Nginx.

## Cách kiểm chứng
Split integrity/leakage tests; schema/version/unknown categories; artifact checksum; model fail/timeout; đặt lịch manual; lưu metrics thật và model card giới hạn.

## Nguồn
Master I §6, phần II H8. scikit-learn chính thức: https://scikit-learn.org/stable/.
