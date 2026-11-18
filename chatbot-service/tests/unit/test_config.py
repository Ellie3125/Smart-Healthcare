import copy

import pytest
import yaml
from pydantic import ValidationError

from app.config_tables import RoutingTable, SpecialtyTable
from tests.conftest import CONFIG_DIR


def load(name):
    with (CONFIG_DIR / name).open(encoding="utf-8") as f:
        return yaml.safe_load(f)


def test_shipped_tables_are_valid(tables):
    assert tables.red_flags and tables.medical_advice
    assert "specialty" in tables.version


def test_specialty_table_is_still_draft_until_reviewed():
    # Nhắc việc: nội dung phải được người có chuyên môn duyệt trước khi bật cho bệnh nhân thật (Phạm vi §7).
    table = SpecialtyTable.model_validate(load("specialty_hints.yaml"))
    assert table.status == "draft" or table.reviewed_by


def test_hint_with_unknown_specialty_is_rejected():
    data = copy.deepcopy(load("specialty_hints.yaml"))
    data["specialty_hints"].append({"specialty": "KHONG_CO", "keywords": ["x"]})
    with pytest.raises(ValidationError, match="KHONG_CO"):
        SpecialtyTable.model_validate(data)


def test_approved_table_needs_reviewer():
    data = copy.deepcopy(load("specialty_hints.yaml"))
    data["status"] = "approved"
    data["reviewed_by"] = None
    with pytest.raises(ValidationError, match="reviewed_by"):
        SpecialtyTable.model_validate(data)


def test_routing_needs_all_templates_and_known_intents():
    data = copy.deepcopy(load("routing.yaml"))
    del data["templates"]["emergency"]
    with pytest.raises(ValidationError, match="emergency"):
        RoutingTable.model_validate(data)

    data = copy.deepcopy(load("routing.yaml"))
    data["intents"]["DELETE_EVERYTHING"] = ["xóa"]
    with pytest.raises(ValidationError):
        RoutingTable.model_validate(data)
