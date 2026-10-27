import os

# ✅ КРИТИЧНО: задаём путь к кэшу моделей без кириллицы
# Это обходит баг PaddleOCR с не-ASCII именами пользователей на Windows
os.environ["PADDLE_PDX_CACHE_HOME"] = r"C:\paddlex_cache"
os.environ["PADDLE_OCR_BASE_DIR"] = r"C:\paddlex_cache\paddleocr"

import json
from paddleocr import PaddleOCR

def recognize_receipt(image_path: str, output_json: str):
    print(f"Загрузка изображения: {image_path}")
    
    ocr = PaddleOCR(
        lang="ru",
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False
    )

    print("Запуск распознавания... (первый раз может занять время на скачивание весов)")
    result = ocr.predict(image_path)

    output_data = []
    for res in result:
        texts = res['rec_texts']
        scores = res['rec_scores']
        polys = res['rec_polys']

        for i in range(len(texts)):
            output_data.append({
                "text": texts[i],
                "confidence": float(scores[i]),
                "bbox": polys[i].tolist()
            })

    with open(output_json, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, ensure_ascii=False, indent=2)

    print(f"✅ Готово! Результат сохранён в {output_json}")
    print(f"📝 Распознано текстовых блоков: {len(output_data)}")

if __name__ == "__main__":
    recognize_receipt("receipt.jpg", "receipt_result.json")