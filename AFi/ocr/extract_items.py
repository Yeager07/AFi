# ============================================================
# extract_items.py
# Извлечение структурированных позиций из результата PaddleOCR
# ============================================================

import json
import re
import difflib


# ------------------------------------------------------------
# Защищённые слова — fuzzy их не трогает никогда
# (реальные бренды/слова, которые похожи на что-то в словаре)
# ------------------------------------------------------------

PROTECTED_WORDS = {
    "ЧЕБУПИЦЦА",
    "ПРОВАНСАЛЬ",
    "ПЕППЕРОНИ",
    "ЮБИЛЕЙНОЕ",
    "ХАЙНЦ",
}


# ------------------------------------------------------------
# Явные OCR-исправления (точное совпадение, до fuzzy)
# Только то, где мы точно знаем, что OCR ошибся
# ------------------------------------------------------------

OCR_FIXES = {
    "НОГУРТ":    "ЙОГУРТ",
    "МОГУРТ":    "ЙОГУРТ",
    "АХАР":      "САХАР",
    "ХАХАР":     "САХАР",
    "МЙОНЕЗ":    "МАЙОНЕЗ",
    "ЧЕБУЛИЦЦА": "ЧЕБУПИЦЦА",   # OCR: П → Л
}


# ------------------------------------------------------------
# Справочник частых товаров для fuzzy-исправлений
# ------------------------------------------------------------

COMMON_PRODUCTS = [
    "МОЛОКО", "ХЛЕБ", "БАТОН", "САХАР", "СОЛЬ", "МАЙОНЕЗ", "КЕТЧУП", "ЙОГУРТ",
    "КЕФИР", "СМЕТАНА", "ТВОРОГ", "СЫР", "МАСЛО", "СОСИСКИ", "КОЛБАСА", "ПЕЛЬМЕНИ",
    "ВОДА", "СОК", "ЧАЙ", "КОФЕ", "ШОКОЛАД", "ПЕЧЕНЬЕ", "КОНФЕТЫ", "МОРОЖЕНОЕ",
    "ЯЙЦА", "КУРИЦА", "МЯСО", "РЫБА", "КАРТОФЕЛЬ", "МОРКОВЬ", "ЛУК", "ПОМИДОРЫ",
    "ОГУРЦЫ", "ЯБЛОКИ", "БАНАНЫ", "АПЕЛЬСИНЫ", "ЧИПСЫ", "СУХАРИКИ", "КРУПА",
    "РИС", "ГРЕЧКА", "МАКАРОНЫ", "МУКА", "ДРОЖЖИ", "УКСУС", "СПЕЦИИ", "ПРИПРАВА",
    "СОУС", "ГОРЧИЦА", "МЁД", "ВАРЕНЬЕ", "ДЖЕМ", "ПРЯНИКИ", "ТОРТ", "ПИРОЖНОЕ",
    "ЧЕБУРЕК", "ПИЦЦА", "САЛАТ", "СУП", "БОРЩ", "ВАРЕНИКИ", "БЛИНЫ",
    "СГУЩЕНКА", "СЛИВКИ", "ПРОСТОКВАША", "РЯЖЕНКА", "АЙРАН",
    "МИНЕРАЛКА", "ЛИМОНАД", "КВАС", "ПИВО", "ВИНО",
    "САЛФЕТКИ", "БУМАГА", "ПАКЕТ", "ПОРОШОК", "МЫЛО", "ШАМПУНЬ", "ПАСТА",
    "ГУБКА", "БАТАРЕЙКИ", "ЛАМПОЧКА", "КЛЕЙ", "СКОТЧ",
    "ПАШТЕТ", "ТУШЕНКА", "САРДЕЛЬКИ", "ШПРОТЫ", "КОНСЕРВЫ",
    "ВАФЛИ", "ЗЕФИР", "ХАЛВА", "БУЛОЧКА", "БАГЕТ", "ЛАВАШ", "ЛЕПЕШКА",
    "КЕТЧУП", "ПЕППЕРОНИ", "ПРОВАНСАЛЬ", "ВЕНСКИЕ", "ПОДМОСКОВНЫЙ",
    "ТОМАТНЫЙ", "ПИТЬЕВОЙ", "ПЕЧЕНАЯ", "ЧЕРНАЯ", "ПЕСОК", "КУРОЧКА", "ИТАЛЬ",
]

COMMON_BRANDS = [
    "ХАЙНЦ", "ПРОВАНСАЛЬ", "ЮБИЛЕЙНОЕ", "СЛОБОДА", "ПРОСТОКВАШИНО",
    "ДОМИК", "ВКУСНОТЕЕВО", "ЧЕРКИЗОВО", "МИРАТОРГ", "МОНЕТКА",
    "МАГНИТ", "ПЯТЁРОЧКА", "ЛЕНТА", "ДИКСИ", "АШАН", "ПЕРЕКРЁСТОК",
    "ОКЕЙ", "ВЕРНЫЙ", "ЯРЧЕ",
]

FUZZY_DICT = list(set(COMMON_PRODUCTS + COMMON_BRANDS))


# ------------------------------------------------------------
# Регексы
# ------------------------------------------------------------

QTY_PATTERN = re.compile(
    r'(\d+[.,]?\d*)\s*[О0]?[ШшSs][ТтT]\s*[XxХх]?\.?',
    re.IGNORECASE
)

VAT_PATTERN = re.compile(
    r'НДС\s*\d+[ОOо0]*\s*[%ЪъЬь]?\.?',
    re.IGNORECASE
)

PRICE_PATTERN = re.compile(r'(\d+\.\d{2})')


# ------------------------------------------------------------
# Нормализация OCR-путаниц
# ------------------------------------------------------------

def normalize_vat(text: str) -> str:
    """HAC10% → НДС10%, НДC10% → НДС10%."""
    text = re.sub(r'[НH][АA][СCсc](?=\d)', 'НДС', text)
    text = re.sub(r'НД[СCcс5](?=\d)', 'НДС', text)
    return text


def normalize_ocr(text: str) -> str:
    """Общие OCR-путаницы в контексте цифр и НДС."""
    text = re.sub(r'(?<=\d)[ОOo](?=\d)', '0', text)
    text = re.sub(r'(?<=\d)[ОOo](?=\d*[ГгКкМмЛл])', '0', text)
    text = normalize_vat(text)
    return text


def clean_edges(text: str) -> str:
    """Срезает мусор по краям названия."""
    text = text.strip('*.,;: \t')
    text = re.sub(r'\s+[XxХх]$', '', text).strip()
    text = re.sub(r'\s+', ' ', text)
    return text


# ------------------------------------------------------------
# Адаптивный fuzzy + OCR_FIXES + PROTECTED_WORDS
# ------------------------------------------------------------

def get_cutoff(word_len: int) -> float:
    """
    Адаптивный порог схожести в зависимости от длины слова.

    Чем короче слово — тем ниже порог, потому что:
      - 1 ошибка в 5-буквенном слове = 0.80 similarity
      - 1 ошибка в 6-буквенном слове = 0.83 similarity
      - 1 ошибка в 10-буквенном слове = 0.90 similarity

    Если порог фиксированный 0.85, короткие слова с 1 ошибкой
    не будут исправлены (ХАХАР → САХАР даёт 0.80).

    Для слов <= 4 символов fuzzy отключён вовсе — слишком высок
    риск ложных срабатываний.
    """
    if word_len <= 4:
        return 1.0        # отключаем fuzzy для очень коротких
    if word_len <= 7:
        return 0.80       # 1 ошибка в 5-7-буквенном слове
    return 0.85           # длинные слова — можно быть строже


def fuzzy_fix_word(word: str) -> str:
    """
    Заменяет слово на ближайшее из словаря.

    Уже корректные слова (которые есть в FUZZY_DICT) не трогаются —
    защита от «исправления» правильных МАСЛО/МЯСО/ПАКЕТ.
    """
    if word in FUZZY_DICT:
        return word  # слово уже корректное

    cutoff = get_cutoff(len(word))
    if cutoff >= 1.0:
        return word  # слишком короткое — не трогаем

    matches = difflib.get_close_matches(word, FUZZY_DICT, n=1, cutoff=cutoff)
    return matches[0] if matches else word


def fix_name(name: str) -> str:
    """
    Порядок обработки слова:
      1. PROTECTED_WORDS — реальные бренды, не трогаем
      2. OCR_FIXES       — известные ошибки, жёсткая замена
      3. FUZZY_DICT      — уже корректное слово, пропускаем
      4. fuzzy           — ищем ближайшее по адаптивному порогу
    """
    words = name.split()
    fixed = []
    for w in words:
        if w in PROTECTED_WORDS:
            fixed.append(w)
        elif w in OCR_FIXES:
            fixed.append(OCR_FIXES[w])
        elif w in FUZZY_DICT:
            fixed.append(w)
        else:
            fixed.append(fuzzy_fix_word(w))
    return " ".join(fixed)


# ------------------------------------------------------------
# Группировка блоков в строки по Y-координате
# ------------------------------------------------------------

def group_into_rows(blocks: list, y_threshold: int = 15) -> list:
    clean = []
    for b in blocks:
        if b['confidence'] < 0.6 or not b['text'].strip():
            continue
        if set(b['text']).issubset({'#', '*', '.', ' '}):
            continue
        ys = [pt[1] for pt in b['bbox']]
        b['y_center'] = sum(ys) / len(ys)
        b['x_min'] = min(pt[0] for pt in b['bbox'])
        clean.append(b)

    clean.sort(key=lambda b: b['y_center'])

    rows = []
    current_row = []
    current_y = None

    for b in clean:
        if current_y is None or abs(b['y_center'] - current_y) <= y_threshold:
            current_row.append(b)
            current_y = sum(item['y_center'] for item in current_row) / len(current_row)
        else:
            current_row.sort(key=lambda item: item['x_min'])
            rows.append(current_row)
            current_row = [b]
            current_y = b['y_center']

    if current_row:
        current_row.sort(key=lambda item: item['x_min'])
        rows.append(current_row)

    return rows


# ------------------------------------------------------------
# Извлечение позиций
# ------------------------------------------------------------

def extract_items(input_json: str, output_json: str):
    with open(input_json, 'r', encoding='utf-8') as f:
        blocks = json.load(f)

    rows = group_into_rows(blocks)

    items = []
    is_product_zone = False

    for row in rows:
        texts = [item['text'] for item in row]
        row_str = " ".join(texts)

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

        name_fixed = fix_name(name)

        items.append({
            "name": name_fixed,
            "name_raw": name,
            "quantity": quantity,
            "price": price,
            "total": total,
        })

    with open(output_json, 'w', encoding='utf-8') as f:
        json.dump(items, f, ensure_ascii=False, indent=2)

    # Отчёт
    print(f"✅ Извлечено товаров: {len(items)}")
    print(f"💰 Сумма позиций: {sum(i['total'] for i in items):.2f} ₽")
    print(f"💾 Результат: {output_json}\n")
    for item in items:
        marker = "  " if item['name'] == item['name_raw'] else "✏️"
        print(f"{marker} {item['name']} | {item['quantity']} × {item['price']} = {item['total']} ₽")
        if item['name'] != item['name_raw']:
            print(f"     было: {item['name_raw']}")


if __name__ == "__main__":
    extract_items("receipt_result.json", "final_items.json")