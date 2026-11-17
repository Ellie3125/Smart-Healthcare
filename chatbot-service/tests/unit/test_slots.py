from datetime import UTC, date, datetime

import pytest

from app.router import route
from app.slots import match_specialty_name, parse_date, today_vietnam

TODAY = date(2026, 11, 17)  # thứ ba


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("hôm nay", date(2026, 11, 17)),
        ("ngày mai", date(2026, 11, 18)),
        ("mai", date(2026, 11, 18)),
        ("sáng mai nhé", date(2026, 11, 18)),
        ("ngày kia", date(2026, 11, 19)),
        ("thứ sáu này", date(2026, 11, 20)),
        ("thu 6", date(2026, 11, 20)),
        ("thứ ba", date(2026, 11, 17)),  # đúng hôm nay
        ("thứ hai", date(2026, 11, 23)),  # đã qua trong tuần: tuần sau
        ("thứ tư tuần sau", date(2026, 11, 25)),
        ("chủ nhật", date(2026, 11, 22)),
        ("20/11", date(2026, 11, 20)),
        ("20-11-2026", date(2026, 11, 20)),
        ("05/01", date(2027, 1, 5)),  # không ghi năm, đã qua trong năm nay
        ("ngày 25", date(2026, 11, 25)),
        ("ngày 3", date(2026, 12, 3)),  # đã qua trong tháng: tháng sau
        ("ngày 2 tháng 12", date(2026, 12, 2)),
    ],
)
def test_parse_date(text, expected):
    assert parse_date(text, TODAY) == expected


@pytest.mark.parametrize("text", ["31/02", "số thứ tự của tôi", "bác sĩ Mai khám không", "xem lịch"])
def test_parse_date_rejects_invalid_or_ambiguous(text):
    # "số thứ tự" không phải "thứ tư"; "bác sĩ Mai" không phải "ngày mai".
    assert parse_date(text, TODAY) is None


def test_today_is_vietnam_date():
    assert today_vietnam(datetime(2026, 11, 17, 17, 30, tzinfo=UTC)) == date(2026, 11, 18)  # 00:30 giờ VN


def test_specialty_by_name_or_alias(tables):
    assert match_specialty_name(tables, "cho tôi khám da liễu") == "DA_LIEU"
    assert match_specialty_name(tables, "Khoa Tiêu hóa: Bệnh lý đường tiêu hóa") == "TIEU_HOA"
    assert match_specialty_name(tables, "kham tai mui hong") == "TAI_MUI_HONG"
    assert match_specialty_name(tables, "đau bụng") is None  # triệu chứng không phải tên khoa


def r(tables, text, state=None):
    return route(tables, state or {}, text, None, TODAY)


def test_booking_sentence_extracts_date_and_specialty(tables):
    routed = r(tables, "đặt lịch khám da liễu thứ sáu này")
    assert (routed.intent, routed.slots) == ("SEARCH_SLOTS", {"service_date": "2026-11-20", "specialty_code": "DA_LIEU"})


def test_naming_a_specialty_means_search(tables):
    routed = r(tables, "Khoa Nội tổng quát: Khám các bệnh nội khoa thường gặp; phù hợp khi chưa rõ chuyên khoa.")
    assert (routed.intent, routed.slots) == ("SEARCH_SLOTS", {"specialty_code": "NOI_TONG_QUAT"})


def test_date_answer_continues_search_and_keeps_specialty(tables):
    state = {"intent": "SEARCH_SLOTS", "stage": "COLLECTING", "slots": {"specialty_code": "TIM_MACH"}}
    routed = r(tables, "thứ sáu", state)
    assert (routed.intent, routed.slots) == ("SEARCH_SLOTS", {"service_date": "2026-11-20", "specialty_code": "TIM_MACH"})


def test_date_without_pending_search_goes_to_model(tables):
    assert r(tables, "thứ sáu").route == "MODEL"


def test_search_button_after_single_suggestion_carries_specialty(tables):
    state = {"intent": "SUGGEST_SPECIALTY", "stage": "DONE", "slots": {"specialty_codes": ["DA_LIEU"]}}
    assert route(tables, state, None, "SEARCH_SLOTS", TODAY).slots == {"specialty_code": "DA_LIEU"}
    two = {"intent": "SUGGEST_SPECIALTY", "stage": "DONE", "slots": {"specialty_codes": ["HO_HAP", "TAI_MUI_HONG"]}}
    assert route(tables, two, None, "SEARCH_SLOTS", TODAY).slots == {}


def test_red_flag_still_wins_during_search(tables):
    state = {"intent": "SEARCH_SLOTS", "stage": "COLLECTING", "slots": {}}
    assert r(tables, "mai, nhưng giờ tôi khó thở quá", state).intent == "EMERGENCY"
