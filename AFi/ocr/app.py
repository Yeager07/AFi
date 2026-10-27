# ============================================================
# app.py — FastAPI-микросервис для OCR чеков
# ============================================================

import io
import os
import tempfile
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from PIL import Image, ImageOps

# Указываем путь к кэшу моделей (обход бага с кириллицей в пути)
os.environ["PADDLE_PDX_CACHE_HOME"] = r"C:\paddlex_cache"
os.environ["PADDLE_OCR_BASE_DIR"] = r"C:\paddlex_cache\paddleocr"

from paddleocr import PaddleOCR

from extract_items import (
    group_into_rows, normalize_ocr, clean_edges, fix_name,
    QTY_PATTERN, VAT_PATTERN, PRICE_PATTERN
)

app = FastAPI(title="AFi OCR Service")

# Максимальная длина длинной стороны. Если больше — уменьшаем.
# 2500 — эмпирический размер, на котором PaddleOCR стабильно
# находит мелкий текст чека и не «захлёбывается» от 4000px.
MAX_SIDE = 2500

print("Инициализация PaddleOCR...")
ocr = PaddleOCR(
    lang="ru",
    use_doc_orientation_classify=False,
    use_doc_unwarping=False,
    use_textline_orientation=False
)
print("PaddleOCR готов к работе.")


@app.post("/api/ocr")
async def recognize_receipt(file: UploadFile = File(...)):
    if not file.content_type or not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="Файл должен быть изображением")

    tmp_path = None
    try:
        # 1. Читаем сырые байты
        contents = await file.read()
        print(f"[OCR] Получен файл: {file.filename}, размер: {len(contents)} байт")

        if len(contents) == 0:
            raise HTTPException(status_code=400, detail="Файл пустой")

        # 2. Открываем через PIL и смотрим метаданные
        image = Image.open(io.BytesIO(contents))
        exif_orientation = image.getexif().get(274, None)
        print(f"[OCR] Размер: {image.size[0]}x{image.size[1]}, "
              f"режим: {image.mode}, EXIF orientation: {exif_orientation}")

        # 3. Разворачиваем по EXIF (если тег есть)
        image = ImageOps.exif_transpose(image)
        if image.mode != "RGB":
            image = image.convert("RGB")
        print(f"[OCR] После exif_transpose: {image.size[0]}x{image.size[1]}")

        # 4. Даунскейл, если разрешение слишком большое
        max_dim = max(image.size)
        if max_dim > MAX_SIDE:
            ratio = MAX_SIDE / max_dim
            new_size = (int(image.size[0] * ratio), int(image.size[1] * ratio))
            image = image.resize(new_size, Image.LANCZOS)
            print(f"[OCR] После даунскейла: {image.size[0]}x{image.size[1]}")

        # 5. Сохраняем в PNG БЕЗ потерь — никаких JPEG-артефактов
        with tempfile.NamedTemporaryFile(delete=False, suffix=".png") as tmp:
            image.save(tmp.name, format="PNG", optimize=False)
            tmp_path = tmp.name
        print(f"[OCR] Временный PNG: {os.path.getsize(tmp_path)} байт")

        # 6. Распознавание
        result = ocr.predict(tmp_path)

        # 7. Разбор
        blocks = []
        for res in result:
            texts = res['rec_texts']
            scores = res['rec_scores']
            polys = res['rec_polys']
            for i in range(len(texts)):
                blocks.append({
                    "text": texts[i],
                    "confidence": float(scores[i]),
                    "bbox": polys[i].tolist()
                })
        print(f"[OCR] Распознано блоков: {len(blocks)}")

        # 8. Парсинг
        rows = group_into_rows(blocks)
        print(f"[OCR] Сгруппировано строк: {len(rows)}")

        items = []
        is_product_zone = False

        for row in rows:
            row_str = " ".join(item['text'] for item in row)

            if "НАИМЕНОВАНИЕ" in row_str:
                is_product_zone = True
                continue
            if "ИТОГ" in row_str or "ОПЛАТА" in row_str:
                is_product_zone = False
                continue
            if not is_product_zone:
                continue

            row_norm = normalize_ocr(row_str)
            prices = PRICE_PATTERN.findall(row_norm)
            if not prices:
                continue

            qty_match = QTY_PATTERN.search(row_norm)
            quantity = 1.0
            if qty_match:
                try:
                    quantity = float(qty_match.group(1).replace(',', '.'))
                except ValueError:
                    pass

            if len(prices) >= 2:
                price = float(prices[-2])
                total = float(prices[-1])
            else:
                price = float(prices[0])
                total = price * quantity

            name = row_norm
            name = QTY_PATTERN.sub('', name)
            name = VAT_PATTERN.sub('', name)
            name = PRICE_PATTERN.sub('', name)
            name = clean_edges(name)

            if len(name) < 2:
                continue

            items.append({
                "name": fix_name(name),
                "quantity": quantity,
                "price": price,
                "total": total,
            })

        print(f"[OCR] Извлечено позиций: {len(items)}")
        return JSONResponse(content={
            "items": items,
            "total": round(sum(i['total'] for i in items), 2)
        })

    except HTTPException:
        raise
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"Ошибка OCR: {str(e)}")
    finally:
        if tmp_path and os.path.exists(tmp_path):
            try: os.unlink(tmp_path)
            except: pass


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)