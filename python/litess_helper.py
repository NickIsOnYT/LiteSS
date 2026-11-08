#!/usr/bin/env python3
"""LiteSS Python bridge helper.

LiteSS starts this script once per request and exchanges a single JSON object over
stdin/stdout. Nothing else is printed to stdout; diagnostics go to stderr.

Requests
--------
OCR (Tesseract through pytesseract):
    {"action": "ocr", "engine": "tesseract", "image_base64": "...", "language": "en"}

Translation:
    {"action": "translate", "engine": "argos", "text": "...", "source": "en", "target": "de"}
    {"action": "translate", "engine": "libretranslate", "text": "...", "source": "auto",
     "target": "de", "url": "http://localhost:5000", "api_key": ""}

Responses
---------
    {"ok": true, "text": "..."}
    {"ok": false, "error": "..."}

Dependencies are only needed for the features you actually use:
    pip install pillow pytesseract              # Python OCR (plus the Tesseract app)
    pip install argostranslate langdetect       # Argos Translate
"""

from __future__ import annotations

import base64
import io
import json
import sys
import urllib.error
import urllib.request

# Our settings use short ISO 639-1 tags; Tesseract expects its own traineddata names.
TESSERACT_LANGUAGES = {
    "auto": "eng",
    "en": "eng",
    "de": "deu",
    "fr": "fra",
    "es": "spa",
    "it": "ita",
    "pt": "por",
    "nl": "nld",
    "pl": "pol",
    "ru": "rus",
    "uk": "ukr",
    "tr": "tur",
    "ja": "jpn",
    "zh": "chi_sim",
    "ko": "kor",
    "ar": "ara",
    "hi": "hin",
}


def main() -> int:
    try:
        raw = sys.stdin.read()
        payload = json.loads(raw) if raw.strip() else {}
        action = payload.get("action")

        if action == "ocr":
            text = run_ocr(payload)
        elif action == "translate":
            text = run_translate(payload)
        else:
            raise ValueError(f"Unknown action: {action!r}")

        print(json.dumps({"ok": True, "text": text}, ensure_ascii=False))
    except Exception as exc:  # noqa: BLE001 - the message is shown to the user
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False))

    return 0


def run_ocr(payload: dict) -> str:
    try:
        from PIL import Image
    except ImportError as exc:
        raise RuntimeError(
            "Pillow is not installed. Run: pip install pillow pytesseract"
        ) from exc

    try:
        import pytesseract
    except ImportError as exc:
        raise RuntimeError(
            "pytesseract is not installed. Run: pip install pillow pytesseract"
        ) from exc

    encoded = payload.get("image_base64") or ""
    if not encoded:
        raise ValueError("image_base64 is required for OCR.")

    image = Image.open(io.BytesIO(base64.b64decode(encoded)))
    if image.mode not in ("RGB", "L"):
        image = image.convert("RGB")

    code = (payload.get("language") or "auto").lower()
    language = (
        TESSERACT_LANGUAGES.get(code)
        or TESSERACT_LANGUAGES.get(code.split("-")[0])
        or "eng"
    )
    config = payload.get("config") or ""

    try:
        return pytesseract.image_to_string(image, lang=language, config=config).strip()
    except pytesseract.TesseractNotFoundError as exc:
        raise RuntimeError(
            "Tesseract is not installed or is not on PATH. "
            "Install it from https://github.com/UB-Mannheim/tesseract/wiki"
        ) from exc
    except pytesseract.TesseractError as exc:
        raise RuntimeError(f"Tesseract failed: {exc}") from exc


def run_translate(payload: dict) -> str:
    engine = (payload.get("engine") or "argos").lower()
    text = payload.get("text") or ""
    source = (payload.get("source") or "auto").lower()
    target = (payload.get("target") or "en").lower()

    if not text.strip():
        return ""

    if engine == "argos":
        return translate_argos(text, source, target)

    if engine == "libretranslate":
        return translate_libretranslate(payload, text, source, target)

    raise ValueError(f"Unknown translation engine: {engine!r}")


def translate_argos(text: str, source: str, target: str) -> str:
    try:
        from argostranslate import translate as argos
    except ImportError as exc:
        raise RuntimeError(
            "Argos Translate is not installed. Run: pip install argostranslate"
        ) from exc

    if source == "auto":
        try:
            from langdetect import detect

            source = detect(text)
        except Exception as exc:  # noqa: BLE001
            raise RuntimeError(
                "Argos Translate needs an explicit source language. "
                "Choose one in LiteSS Settings (or pip install langdetect)."
            ) from exc

    translated = argos.translate(text, source, target)
    if not translated:
        raise RuntimeError(
            f"No Argos Translate model is installed for {source} -> {target}. "
            "Install one with: python -m argostranslate.package"
        )

    return translated


def translate_libretranslate(payload: dict, text: str, source: str, target: str) -> str:
    url = (payload.get("url") or "").rstrip("/")
    if not url:
        raise ValueError("LibreTranslate URL is not configured.")

    body = {"q": text, "source": source or "auto", "target": target, "format": "text"}
    api_key = payload.get("api_key") or ""
    if api_key:
        body["api_key"] = api_key

    request = urllib.request.Request(
        url + "/translate",
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json", "User-Agent": "LiteSS/1.0"},
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            data = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", "replace")
        raise RuntimeError(f"LibreTranslate returned HTTP {exc.code}: {detail}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Could not reach LibreTranslate: {exc.reason}") from exc

    translated = data.get("translatedText")
    if not translated:
        raise RuntimeError(data.get("error") or "LibreTranslate returned no text.")

    return translated


if __name__ == "__main__":
    sys.exit(main())
