"""Chat thử với chatbot trên terminal, đóng vai API chat của .NET (E4, contract docs/api/chatbot.md §3) trong lúc web bệnh nhân
chưa nối chatbot. Chỉ dùng trên máy dev hoặc khi demo.

    CHATBOT_INTERNAL_KEY=<khóa của dịch vụ chatbot> uv run python scripts/demo_chat.py --user patient01

Đăng nhập bệnh nhân qua Api (/auth/patient/login), chọn hồ sơ, rồi gửi từng câu sang chatbot kèm X-Chatbot-Key và token của
bệnh nhân — đúng những gì .NET sẽ làm. Lệnh: /1../7 bấm nút gợi ý, /new cuộc trò chuyện mới, /q thoát.
Mật khẩu lấy từ biến DEMO_PASSWORD hoặc hỏi khi chạy; không in ra.
"""

from __future__ import annotations

import argparse
import base64
import getpass
import json
import os
import sys
import time
import uuid

import httpx

SUGGESTIONS = {
    "/1": ("SEARCH_SLOTS", "Tìm lịch khám"),
    "/2": ("VIEW_APPOINTMENTS", "Xem lịch của tôi"),
    "/3": ("VIEW_TICKET", "Xem vé"),
    "/4": ("VIEW_EXAM_PAYMENT", "Thanh toán khám"),
    "/5": ("RESCHEDULE", "Đổi lịch"),
    "/6": ("CANCEL", "Hủy lịch"),
    "/7": ("SUGGEST_SPECIALTY", "Tôi nên khám khoa nào"),
}


def user_id_from(token: str) -> str:
    payload = token.split(".")[1]
    claims = json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))
    for key in ("sub", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"):
        if claims.get(key):
            return claims[key]
    sys.exit("Token không có sub.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Chat thử với chatbot (đóng vai .NET)")
    parser.add_argument("--api", default="http://localhost:5000")
    parser.add_argument("--chatbot", default="http://localhost:8000")
    parser.add_argument("--user", default="patient01")
    args = parser.parse_args()
    key = os.environ.get("CHATBOT_INTERNAL_KEY") or sys.exit("Thiếu CHATBOT_INTERNAL_KEY (trùng khóa của dịch vụ chatbot).")
    password = os.environ.get("DEMO_PASSWORD") or getpass.getpass(f"Mật khẩu của {args.user}: ")

    with httpx.Client(timeout=180) as http:
        login = http.post(f"{args.api}/auth/patient/login", json={"userName": args.user, "password": password})
        if login.status_code != 200:
            sys.exit(f"Đăng nhập lỗi {login.status_code}: {login.text[:200]}")
        token = login.json()["accessToken"]
        user_id = user_id_from(token)
        auth = {"Authorization": f"Bearer {token}"}

        profiles = http.get(f"{args.api}/api/patient/profiles", headers=auth).json()
        for i, p in enumerate(profiles, start=1):
            print(f"  {i}. {p.get('fullName')} ({p.get('relationship', '')})")
        choice = input("Chọn hồ sơ [1]: ").strip() or "1"
        patient_id = profiles[int(choice) - 1]["patientId"]

        health = http.get(f"{args.chatbot}/health").json()
        print(f"\nChatbot: model {health['model']} {'SẴN SÀNG' if health['available'] else 'TẠM NGƯNG (chỉ đường tắt)'}")
        print("Nút: " + "  ".join(f"{k} {label}" for k, (_, label) in SUGGESTIONS.items()) + "  /new  /q\n")

        conversation = str(uuid.uuid4())
        while True:
            try:
                text = input("Bạn: ").strip()
            except (EOFError, KeyboardInterrupt):
                break
            if not text:
                continue
            if text == "/q":
                break
            if text == "/new":
                conversation = str(uuid.uuid4())
                print("(cuộc trò chuyện mới)\n")
                continue
            content = {"suggestion": SUGGESTIONS[text][0]} if text in SUGGESTIONS else {"text": text}
            body = {"conversationId": conversation, "userId": user_id, "patientId": patient_id,
                    "messageId": str(uuid.uuid4()), **content}
            started = time.perf_counter()
            r = http.post(f"{args.chatbot}/v1/turns", json=body, headers={**auth, "X-Chatbot-Key": key})
            elapsed = time.perf_counter() - started
            if r.status_code != 200:
                print(f"Bot [{r.status_code} {r.json().get('code')}]: {r.json().get('title')}  ({elapsed:.1f}s)\n")
                continue
            data = r.json()
            print(f"Bot: {data['reply']}")
            for i, o in enumerate(data["options"], start=1):
                print(f"   [{i}] {o['label']}")
            if data["suggestions"]:
                print("   gợi ý: " + ", ".join(data["suggestions"]))
            print(f"   ({data.get('intent') or 'không intent'}, {elapsed:.1f}s)\n")


if __name__ == "__main__":
    main()
