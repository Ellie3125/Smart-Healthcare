from datetime import date

import pytest

from app.replies import shortcut_reply
from app.router import route


TODAY = date(2026, 11, 17)  # thứ ba


def r(tables, text, state=None):
    return route(tables, state or {}, text, None, TODAY)


@pytest.mark.parametrize(
    "text",
    [
        "Tôi bị đau ngực và khó thở, nên khám khoa nào?",  # khớp cả intent lẫn chuyên khoa: nguy hiểm vẫn thắng
        "dau nguc qua",  # không dấu
        "bố tôi bị co giật",
        "con tôi đau bụng rồi ngất xỉu",  # khớp Nhi, Tiêu hóa nhưng có dấu hiệu nguy hiểm
        "đau ngực, cho tôi đặt lịch tim mạch",
    ],
)
def test_red_flags_always_win(tables, text):
    routed = r(tables, text)
    assert (routed.route, routed.intent) == ("SHORTCUT", "EMERGENCY")
    response, state = shortcut_reply(tables, routed)
    assert "115" in response.reply
    assert response.suggestions == [] and response.options == []  # không gợi ý đặt lịch
    assert state["stage"] == "ABORTED"


@pytest.mark.parametrize(
    "text",
    ["Tôi đau bụng thì uống thuốc gì?", "kê đơn cho tôi thuốc ho", "tôi bị bệnh gì vậy", "có phải bị viêm dạ dày không"],
)
def test_medical_advice_is_refused(tables, text):
    routed = r(tables, text)
    assert routed.intent == "OUT_OF_SCOPE"
    response, _ = shortcut_reply(tables, routed)
    assert "không thể tư vấn" in response.reply
    assert response.options == []


def test_symptom_suggests_specialty_from_table_with_disclaimer(tables):
    routed = r(tables, "tôi bị đau bụng trên, đầy hơi mấy hôm nay")
    assert (routed.intent, routed.specialty_codes) == ("SUGGEST_SPECIALTY", ["TIEU_HOA"])
    response, state = shortcut_reply(tables, routed)
    assert "Khoa Tiêu hóa" in response.reply
    assert tables.specialties.disclaimer in response.reply
    assert [o.specialty_code for o in response.options] == ["TIEU_HOA"]
    assert response.suggestions == ["SEARCH_SLOTS"]  # bệnh nhân tự chọn rồi tìm lịch; chatbot không tự đặt
    # Mô tả triệu chứng không vào State, chỉ mã chuyên khoa.
    assert state == {"intent": "SUGGEST_SPECIALTY", "stage": "DONE", "slots": {"specialty_codes": ["TIEU_HOA"]}}


def test_specialty_ranking_caps_at_max(tables):
    routed = r(tables, "con tôi bị ho, sổ mũi và đau họng")
    assert routed.specialty_codes == ["TAI_MUI_HONG", "HO_HAP"]  # điểm cao trước, tối đa 2


def test_ngat_mui_is_ent_not_emergency(tables):
    routed = r(tables, "tôi bị ngạt mũi")
    assert (routed.intent, routed.specialty_codes) == ("SUGGEST_SPECIALTY", ["TAI_MUI_HONG"])


def test_asking_which_specialty_without_symptoms_asks_for_description(tables):
    routed = r(tables, "tôi nên khám khoa nào?")
    assert (routed.intent, routed.has_symptoms) == ("SUGGEST_SPECIALTY", False)
    response, state = shortcut_reply(tables, routed)
    assert response.options == []
    assert (state["stage"], state["missing_slots"]) == ("COLLECTING", ["symptom"])


def test_answer_after_asking_falls_back_to_full_list_when_no_match(tables):
    state = {"intent": "SUGGEST_SPECIALTY", "stage": "COLLECTING"}
    routed = r(tables, "người cứ thấy không ổn", state)
    assert (routed.intent, routed.has_symptoms, routed.specialty_codes) == ("SUGGEST_SPECIALTY", True, [])
    response, _ = shortcut_reply(tables, routed)
    assert [o.specialty_code for o in response.options] == [s.code for s in tables.specialties.specialties]
    assert tables.specialties.disclaimer in response.reply


def test_specialty_names_only_come_from_table(tables):
    codes = {s.code for s in tables.specialties.specialties}
    for text in ["đau bụng", "ngứa da", "ho kéo dài", "tim đập nhanh", "sốt", "đau họng"]:
        routed = r(tables, text)
        assert routed.specialty_codes and set(routed.specialty_codes) <= codes


@pytest.mark.parametrize(
    ("text", "intent"),
    [
        ("xem lịch của tôi", "VIEW_APPOINTMENTS"),
        ("tôi muốn hủy lịch hẹn", "CANCEL"),
        ("huy lich", "CANCEL"),
        ("cho tôi đổi lịch sang thứ sáu", "RESCHEDULE"),
        ("số thứ tự của tôi là bao nhiêu", "VIEW_TICKET"),
        ("tôi đã thanh toán chưa", "VIEW_EXAM_PAYMENT"),
        ("xem lịch sử khám của tôi", "VIEW_VISIT_HISTORY"),
        ("lần khám trước của con tôi", "VIEW_VISIT_HISTORY"),
        ("đặt lịch khám thứ sáu này", "SEARCH_SLOTS"),
        ("xin chào", "HELP"),
    ],
)
def test_intent_keywords(tables, text, intent):
    assert r(tables, text).intent == intent


def test_free_text_goes_to_model(tables):
    assert r(tables, "bác sĩ Nam có khám chiều nay không nhỉ").route == "MODEL"


def test_suggestion_button_bypasses_text_rules(tables):
    routed = route(tables, {}, None, "VIEW_TICKET", TODAY)
    assert (routed.route, routed.intent) == ("SHORTCUT", "VIEW_TICKET")

