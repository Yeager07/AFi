import json
import re

def parse_receipt(input_json: str, output_json: str):
    with open(input_json, 'r', encoding='utf-8') as f:
        blocks = json.load(f)

    # 1. Фильтруем мусор (низкая уверенность, пустые строки, решётки от QR)
    clean_blocks = []
    for b in blocks:
        if b['confidence'] < 0.6:  # отсекаем неуверенные символы
            continue
        if not b['text'].strip():
            continue
        if set(b['text']).issubset({'#', '*', '.', ' '}):  # отсекаем решётки
            continue
        clean_blocks.append(b)

    # 2. Группируем по Y-координате (строки)
    # Вычисляем центр по Y для каждого блока
    for b in clean_blocks:
        ys = [pt[1] for pt in b['bbox']]
        b['y_center'] = sum(ys) / len(ys)
        b['x_min'] = min(pt[0] for pt in b['bbox'])

    # Сортируем по Y
    clean_blocks.sort(key=lambda b: b['y_center'])

    rows = []
    current_row = []
    current_y = None
    Y_THRESHOLD = 15  # пикселей: если центры Y ближе, это одна строка

    for b in clean_blocks:
        if current_y is None or abs(b['y_center'] - current_y) <= Y_THRESHOLD:
            current_row.append(b)
            # Обновляем средний Y строки (чтобы избежать дрейфа)
            current_y = sum(item['y_center'] for item in current_row) / len(current_row)
        else:
            # Сортируем текущую строку по X и добавляем в общий список
            current_row.sort(key=lambda item: item['x_min'])
            rows.append(current_row)
            current_row = [b]
            current_y = b['y_center']

    if current_row:
        current_row.sort(key=lambda item: item['x_min'])
        rows.append(current_row)

    # 3. Выводим результат для визуальной проверки
    print(f"Сгруппировано строк: {len(rows)}\n")
    
    parsed_items = []
    for i, row in enumerate(rows):
        texts = [item['text'] for item in row]
        # Печатаем строку для отладки
        print(f"Строка {i+1:02d}: {texts}")
        
        # Простая эвристика: если в строке есть цена с копейками (123.45), 
        # и это не "ИТОГ" и не "ОПЛАТА" — считаем это товаром
        row_text = " ".join(texts)
        if re.search(r'\d+\.\d{2}', row_text) and not any(k in row_text for k in ["ИТОГ", "СУММА", "ОПЛАТА", "НДС"]):
             # Пока просто сохраняем всю строку, чтобы посмотреть, как она выглядит
             parsed_items.append({
                 "raw_row": texts,
                 # Можно попробовать вытащить сумму (последний блок справа)
                 "assumed_price": [t for t in texts if re.match(r'^\d+\.\d{2}$', t)]
             })

    with open(output_json, 'w', encoding='utf-8') as f:
        json.dump(parsed_items, f, ensure_ascii=False, indent=2)
    print(f"\n✅ Позиции сохранены в {output_json}")

if __name__ == "__main__":
    parse_receipt("receipt_result.json", "parsed_receipt.json")