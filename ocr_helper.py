from difflib import SequenceMatcher
import json
import os
import re
import sys
import tempfile

STAT_KEYS = ('elims', 'objective', 'damage', 'healing', 'deaths')
STAT_NUMBER_RE = re.compile(
    r'(?<![\w:])(?:\d+:\d{1,2}|\d{1,3}(?:,\d{3})+|\d+)(?![\w:])'
)


def _number(value, objective=False):
    text = value.replace(' ', '').replace(',', '')
    if objective and ':' in text:
        minutes, seconds = text.split(':', 1)
        if not minutes.isdigit() or not seconds.isdigit() or int(seconds) >= 60:
            raise ValueError(value)
        return int(minutes) * 60 + int(seconds)
    if not text.isdigit():
        raise ValueError(value)
    return int(text)


def _normalize_name(value):
    return ''.join(char for char in value.casefold() if char.isalnum())


def _row_name_score(row, name):
    clean_name = _normalize_name(name)
    clean_row = _normalize_name(row)
    if not clean_name:
        return 0
    if clean_name in clean_row:
        return 1
    words = [_normalize_name(word) for word in re.findall(r'[\w.-]+', row)]
    return max((SequenceMatcher(None, clean_name, word).ratio() for word in words if word), default=0)


def extract_stats_from_rows(rows, player_names):
    candidates = []
    for index, row in enumerate(rows):
        numbers = STAT_NUMBER_RE.findall(row)
        if len(numbers) >= len(STAT_KEYS):
            candidates.append((index, row, numbers[-len(STAT_KEYS):]))

    found = {}
    used_rows = set()
    for player_index, name in enumerate(player_names):
        ranked = sorted(
            ((_row_name_score(row, name), row_index, numbers) for row_index, row, numbers in candidates),
            reverse=True,
        )
        for score, row_index, numbers in ranked:
            if score < 0.62 or row_index in used_rows:
                continue
            values = [_number(value, objective=(i == 1)) for i, value in enumerate(numbers)]
            found[player_index] = dict(zip(STAT_KEYS, values))
            used_rows.add(row_index)
            break
    return found


def _rows_from_pages(pages):
    rows = []
    for page in pages:
        current = []
        current_y = None
        tolerance = 8
        for item in sorted(page.text_items, key=lambda value: (value.y, value.x)):
            text = item.text.strip()
            if not text:
                continue
            center_y = item.y + item.height / 2
            if current and abs(center_y - current_y) > tolerance:
                rows.append(' '.join(value.text.strip() for value in sorted(current, key=lambda value: value.x)))
                current = []
            current.append(item)
            current_y = center_y if current_y is None else (current_y + center_y) / 2
            tolerance = max(8, item.height * 0.8)
        if current:
            rows.append(' '.join(value.text.strip() for value in sorted(current, key=lambda value: value.x)))
    return rows


def parse_scoreboard_screenshot(path, player_names):
    from PIL import Image
    from liteparse import LiteParse

    with tempfile.TemporaryDirectory() as temp_dir:
        pdf_path = os.path.join(temp_dir, 'scoreboard.pdf')
        with Image.open(path) as image:
            image.seek(0)
            image.convert('RGB').save(pdf_path, 'PDF', resolution=200)
        result = LiteParse(
            ocr_enabled=True,
            ocr_language='eng',
            output_format='text',
            quiet=True,
            num_workers=1,
            dpi=200,
        ).parse(pdf_path)

    rows = _rows_from_pages(result.pages)
    rows.extend(line.strip() for line in result.text.splitlines() if line.strip())
    return extract_stats_from_rows(list(dict.fromkeys(rows)), player_names), result.text


def run_cli():
    output_path = sys.argv[3]
    try:
        results, text = parse_scoreboard_screenshot(sys.argv[1], json.loads(sys.argv[2]))
        payload = {'results': results, 'text': text}
        exit_code = 0
    except Exception as error:
        payload = {'error': str(error)}
        exit_code = 1
    with open(output_path, 'w', encoding='utf-8') as output:
        json.dump(payload, output, ensure_ascii=False)
    return exit_code


if __name__ == '__main__':
    raise SystemExit(run_cli())
