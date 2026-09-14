"""Read-only Python oracle: extracts functions via AST, never imports the Flask app.

Prints fixtures to stdout; --check compares with the committed JSON. All SQL uses
in-memory cursor stubs. No database, server, environment secret or original file
is modified. Requires PyJWT and python-dateutil from the legacy environment.
"""
import ast
import hashlib
import json
import logging
import sys
import warnings
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import jwt
from dateutil import parser as dateutil_parser

warnings.simplefilter("ignore", jwt.warnings.InsecureKeyLengthWarning)
logging.disable(logging.CRITICAL)
NOW = datetime(2026, 9, 8, 12, tzinfo=timezone.utc)
SECRET = "curta"
PASSWORD = "senha-fixture"


class FrozenDatetime(datetime):
    @classmethod
    def now(cls, tz=None):
        return NOW.astimezone(tz) if tz else NOW.replace(tzinfo=None)

    @classmethod
    def utcnow(cls):
        return NOW.replace(tzinfo=None)


class Headers(dict):
    def add(self, key, value):
        self[key] = value


class Response:
    def __init__(self, body):
        self.body = body
        self.headers = Headers()


class Cursor:
    def __init__(self, rows):
        self.rows = rows

    def execute(self, *args):
        pass

    def fetchone(self):
        return (1,)

    def fetchall(self):
        return self.rows

    def close(self):
        pass


source = Path(__file__).resolve().parents[4].parent / "routes" / "api.py"
tree = ast.parse(source.read_text(encoding="utf-8"))
names = {
    "_validate_api_jwt", "_ecodoppler_cors_headers", "_ecodoppler_normalidades_core",
    "_scripts_workflow_key", "_script_download_filename_suffix", "_attach_script_download_headers",
    "_mrd_extension", "_mrd_stem_from_nome", "_mrd_zip_entries",
}
functions = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in names]
assert len(functions) == len(names)
scope = {
    "datetime": FrozenDatetime, "jwt": jwt, "dateutil_parser": dateutil_parser,
    "jsonify": Response, "logger": logging.getLogger("oracle"),
    "Config": SimpleNamespace(API_JWT_SECRET=SECRET, API_JWT_PASSWORD=PASSWORD,
                              API_JWT_DATETIME_TOLERANCE_HOURS=4),
}
exec(compile(ast.Module(body=functions, type_ignores=[]), str(source), "exec"), scope)


def auth_case(name, payload=None, *, secret=SECRET, alg="HS256", header=None, now=NOW):
    if header is None:
        header = "Bearer " + jwt.encode(payload, secret, algorithm=alg)
    scope["request"] = SimpleNamespace(headers={} if header == "<missing>" else {"Authorization": header})
    global NOW
    previous = NOW
    NOW = now
    try:
        with patch("jwt.api_jwt.datetime", FrozenDatetime):
            result = scope["_validate_api_jwt"]()
    finally:
        NOW = previous
    return {"name": name, "now": now.isoformat(), "secret": SECRET, "authorization": None if header == "<missing>" else header,
            "status": 200 if result is None else result[1], "body": None if result is None else result[0].body}


base = {"senha": PASSWORD, "datahora": "2026-09-08T12:00:00Z"}
auth = [auth_case("raw-short-key", base), auth_case("missing-header", header="<missing>"),
        auth_case("empty-header", header=""), auth_case("whitespace-header", header="   "),
        auth_case("malformed", header="a.b.c"), auth_case("wrong-signature", base, secret="outra"),
        auth_case("HS384-rejected", base, alg="HS384"), auth_case("HS512-rejected", base, alg="HS512"),
        auth_case("none-rejected", base, secret="", alg="none"),
        auth_case("raw-token", header=jwt.encode(base, SECRET, algorithm="HS256")),
        auth_case("trimmed-bearer", header="  Bearer " + jwt.encode(base, SECRET, algorithm="HS256") + "  ")]
for name, changes in [
    ("naive-utc", {"datahora": "2026-09-08T11:59:59"}),
    ("offset-minus-three", {"datahora": "2026-09-08T09:00:00-03:00"}),
    ("offset-plus-three", {"datahora": "2026-09-08T15:00:00+03:00"}),
    ("microseconds", {"datahora": "2026-09-08T11:59:59.123456Z"}),
    ("submicrosecond-truncation", {"datahora": "2026-09-08T12:00:00.0000001Z"}),
    ("future", {"datahora": "2026-09-08T12:00:00.000001Z"}),
    ("tolerance-boundary", {"datahora": "2026-09-08T08:00:00Z"}),
    ("tolerance-exceeded", {"datahora": "2026-09-08T07:59:59.999999Z"}),
    ("wrong-password", {"senha": "errada"}),
    ("password-alias", {"senha": "", "password": PASSWORD}),
    ("password-primary-wins", {"senha": "errada", "password": PASSWORD}),
    ("datetime-alias", {"datahora": "", "datetime": "2026-09-08T12:00:00Z"}),
    ("datetime-primary-wins", {"datahora": "inválida", "datetime": "2026-09-08T12:00:00Z"}),
    ("datetime-missing", {"datahora": None}),
    ("datetime-number", {"datahora": 12}),
    ("exp-future", {"exp": int(NOW.timestamp()) + 1}),
    ("exp-boundary", {"exp": int(NOW.timestamp())}),
    ("exp-past", {"exp": int(NOW.timestamp()) - 1}),
    ("exp-string", {"exp": str(int(NOW.timestamp()) + 1)}),
    ("exp-float-truncated", {"exp": NOW.timestamp() + .9}),
    ("exp-invalid", {"exp": "amanhã"}),
    ("nbf-future", {"nbf": int(NOW.timestamp()) + 1}),
    ("iat-future", {"iat": int(NOW.timestamp()) + 1}),
    ("unexpected-audience", {"aud": "partner"}),
    ("invalid-subject", {"sub": 1}),
    ("invalid-jti", {"jti": 1}),
]:
    auth.append(auth_case(name, base | changes))
auth.append(auth_case("previous-utc-day", base | {"datahora": "2026-09-07T23:59:59Z"},
                      now=datetime(2026, 9, 8, 0, 30, tzinfo=timezone.utc)))
auth.append(auth_case("offset-previous-local-day-current-utc", base | {"datahora": "2026-09-07T21:30:00-03:00"},
                      now=datetime(2026, 9, 8, 0, 30, tzinfo=timezone.utc)))
for name, secret in [("utf8-short", "çã🔑"), ("long-raw", "s" * 40)]:
    scope["Config"].API_JWT_SECRET = secret
    case = auth_case(name, base, secret=secret)
    case["secret"] = secret
    auth.append(case)
scope["Config"].API_JWT_SECRET = SECRET


def eco_case(name, rows):
    cursor = Cursor(rows)
    scope["get_db_connection"] = lambda: SimpleNamespace(cursor=lambda: cursor, close=lambda: None)
    result = scope["_ecodoppler_normalidades_core"](1)
    assert isinstance(result, Response), result
    return {"name": name, "rows": rows, "expected": result.body}


def row(minimum=0, maximum=100, classification=None, sex="M", page=12, title="Fonte", year=2020):
    return ["VR_AO", sex, minimum, maximum, None, None, page, classification, title, year]


eco = [eco_case("first-unclassified-wins", [row(), row(10, 20)]),
       eco_case("null-min-no-zones", [row(None, 20)]), eco_case("null-max-no-zones", [row(10, None)]),
       eco_case("zero-metadata-empty-sex", [row(sex="", page=0, title="", year=0)]),
       eco_case("classified-overwrites-default", [row(), row(10, 20, "Normal"), row(30, 40)]),
       eco_case("classified-last-wins", [row(0, 10, "MODERADO"), row(10, 20, "MODERADO")]),
       eco_case("no-quartiles-with-explicit-zone", [row(), row(10, 20, "Alto")])]
for classification in ["Normal", "Leve", "Moderado", "Grave", "Baixo", "Elevado", "Alto", "MODERADA", "LOW", "MODERATED", "ELEVATED", "HIGH"]:
    eco.append(eco_case("classification-" + classification, [row(classification=classification)]))

dates = []
for micros in [0, 1, 100000, 123456, 999999]:
    date = datetime(2026, 9, 8, 9, 10, 11, micros)
    text = date.isoformat()
    dates.append({"microseconds": micros, "iso": text,
                  "workflow_key": scope["_scripts_workflow_key"]({"codscriptlaudo": 42, "data_verificacao": text})})
headers = []
for active, version in [(False, None), (True, None), (True, "1.2 / RC")]:
    response = scope["_attach_script_download_headers"](Response(None), version, active)
    headers.append({"active": active, "version": version, "expected": response.headers})

result = {"source": "../routes/api.py (AST; no application import)", "pyjwt": jwt.__version__,
          "password": PASSWORD, "tolerance_hours": 4, "auth": auth, "dates": dates, "ecodoppler": eco,
          "download_headers": headers,
          "legacy_hashed_token": jwt.encode(base, hashlib.sha256(SECRET.encode()).digest(), algorithm="HS256")}
if "--check" in sys.argv:
    expected = json.loads(Path(__file__).with_name("python_reference.json").read_text(encoding="utf-8"))
    assert expected == result, "Python reference changed; inspect before regenerating fixtures."
    print(f"Python reference OK: {len(auth)} JWT, {len(eco)} Ecodo, {len(dates)} dates, {len(headers)} headers")
else:
    print(json.dumps(result, ensure_ascii=True, indent=2))
