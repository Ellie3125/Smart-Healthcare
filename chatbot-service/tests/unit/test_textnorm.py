from app.textnorm import Message, Phrase, accented, plain


def test_plain_strips_vietnamese_marks_and_d():
    assert plain("Đau BỤNG, ợ chua!") == "dau bung o chua"
    assert accented("Đau BỤNG, ợ chua!") == "đau bụng ợ chua"


def test_accented_message_matches_only_accented_phrase():
    # "ngạt mũi" không được khớp dấu hiệu nguy hiểm "ngất".
    assert not Message.of("Tôi bị ngạt mũi").matches(Phrase.of("ngất"))
    assert Message.of("Hôm qua tôi bị ngất").matches(Phrase.of("ngất"))


def test_unaccented_message_matches_plain_phrase():
    assert Message.of("toi bi dau bung tren").matches(Phrase.of("đau bụng"))


def test_whole_word_match_only():
    # "ho" (ho) không khớp bên trong "họng" hay "hoc".
    assert not Message.of("đau họng").matches(Phrase.of("ho"))
    assert not Message.of("hoc tap").matches(Phrase.of("ho"))
    assert Message.of("tôi bị ho mấy hôm").matches(Phrase.of("ho"))
