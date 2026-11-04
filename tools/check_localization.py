#!/usr/bin/env python3
"""Static localization checks; standard library only. Run from any directory.

This is a focused source audit, not a C# compiler: dynamic translation arguments
are counted as skipped. Literal ordinary/verbatim/interpolated strings are checked.
"""
from collections import Counter
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ERRORS = []
WARNINGS = []


def report(path, text, message, offset=0, warning=False):
    line = text.count('\n', 0, offset) + 1
    (WARNINGS if warning else ERRORS).append(f'{path.relative_to(ROOT)}:{line}: {message}')


def literal(text, start):
    """Read a C# string, including interpolation expressions with nested strings."""
    match = re.match(r'(\$@|@\$|\$|@)?"', text[start:])
    if not match:
        return None
    prefix = match.group(1) or ''
    if text[start + len(match.group(0)):].startswith('""'):
        return None  # Raw literals require a full parser; reported as skipped.
    pos = start + len(match.group(0))
    body = []
    while pos < len(text):
        char = text[pos]
        if char == '"':
            if '@' in prefix and text[pos:pos + 2] == '""':
                body.append('"'); pos += 2; continue
            return ''.join(body), '$' in prefix, pos + 1
        if char == '\\' and '@' not in prefix:
            body.append(text[pos:pos + 2]); pos += 2; continue
        if '$' in prefix and char == '{' and text[pos:pos + 2] != '{{':
            end = expression_end(text, pos + 1)
            body.append(text[pos:end + 1]); pos = end + 1; continue
        if '$' in prefix and text[pos:pos + 2] in ('{{', '}}'):
            body.append(text[pos:pos + 2]); pos += 2; continue
        body.append(char); pos += 1
    return None


def expression_end(text, pos):
    depth = 1
    while pos < len(text):
        item = literal(text, pos)
        if item:
            pos = item[2]; continue
        if text[pos] == '{': depth += 1
        if text[pos] == '}':
            depth -= 1
            if depth == 0: return pos
        pos += 1
    raise ValueError('unclosed interpolation')


def placeholders(body, interpolated):
    if not interpolated:
        # Ignore escaped braces, compare index multiplicities, allow reordering.
        plain = body.replace('{{', '').replace('}}', '')
        return Counter(re.findall(r'\{(\d+)(?:[^{}]*)\}', plain))
    fields = []
    pos = 0
    while pos < len(body):
        if body[pos:pos + 2] in ('{{', '}}'):
            pos += 2; continue
        if body[pos] == '{':
            end = expression_end(body, pos + 1)
            # Formatting/alignment differences do not change the expression used.
            field = re.split(r'[:,](?![^()]*\))', body[pos + 1:end], maxsplit=1)[0]
            fields.append(re.sub(r'\s+', '', field))
            pos = end + 1; continue
        pos += 1
    return Counter(fields)


def source_code_only(text):
    """Blank comments while retaining strings and offsets for diagnostics."""
    result = list(text)
    pos = 0
    while pos < len(text):
        item = literal(text, pos)
        if item:
            pos = item[2]; continue
        if text[pos:pos + 2] == '//':
            end = text.find('\n', pos)
            end = len(text) if end < 0 else end
        elif text[pos:pos + 2] == '/*':
            end = text.find('*/', pos + 2)
            end = len(text) if end < 0 else end + 2
        else:
            pos += 1; continue
        for index in range(pos, end):
            if text[index] != '\n': result[index] = ' '
        pos = end
    return ''.join(result)


def main():
    catalog_path = ROOT / 'src/WavyFi/Localization/UiText.cs'
    catalog = catalog_path.read_text(encoding='utf-8-sig')
    keys = re.findall(r'"([^"\n]+)"\s*=>\s*Locale\.T\(', catalog)
    if len(keys) != len(set(keys)):
        report(catalog_path, catalog, 'duplicate catalog key')
    references = set()
    allowed = {'WavyFi', 'English', 'A', 'A+', 'A−', 'WiFi', 'Wi-Fi',
               'SSID', 'BSSID', 'RSSI', 'WPS', 'MAC', 'CSV', 'dBm', 'GHz',
               'MHz', 'Mbps', '2.4 GHz', '5 GHz', '6 GHz'}
    for path in sorted((ROOT / 'src').rglob('*.xaml')):
        if {'bin', 'obj'} & set(path.parts): continue
        text = path.read_text(encoding='utf-8-sig')
        try:
            tree = ET.fromstring(text)
        except ET.ParseError as exc:
            report(path, text, f'invalid XAML/XML: {exc}'); continue
        for node in tree.iter():
            for attr, value in node.attrib.items():
                ref = re.fullmatch(r'\{loc:Text\s+([^}\s]+)\}', value)
                if ref:
                    key = ref.group(1); references.add(key)
                    if key not in keys: report(path, text, f'missing catalog key: {key}', text.find(value))
                if attr.split('}')[-1] in {'Text', 'Content', 'Header', 'ToolTip', 'Title'}:
                    if (not value.startswith('{') and re.search('[A-Za-z]', value)
                            and not re.search('[\u3400-\u9fff]', value) and value not in allowed):
                        report(path, text, f'review visible English literal: {value!r}', text.find(value), True)
    for key in sorted(set(keys) - references):
        report(catalog_path, catalog, f'unreferenced catalog key: {key}', warning=True)
    checked = skipped = 0
    for path in sorted((ROOT / 'src').rglob('*.cs')):
        if {'bin', 'obj'} & set(path.parts): continue
        text = path.read_text(encoding='utf-8-sig')
        code = source_code_only(text)
        # Includes static imports of T/F, plus explicitly qualified Locale calls.
        for match in re.finditer(r'(?<![\w.])(?:Locale\.)?([TF])\s*\(', code):
            pos = match.end()
            while pos < len(code) and code[pos].isspace(): pos += 1
            first = literal(code, pos)
            if not first: skipped += 1; continue
            pos = first[2]
            while pos < len(code) and code[pos].isspace(): pos += 1
            if pos >= len(code) or code[pos] != ',': skipped += 1; continue
            pos += 1
            while pos < len(code) and code[pos].isspace(): pos += 1
            second = literal(code, pos)
            if not second: skipped += 1; continue
            checked += 1
            if not first[0].strip() or not second[0].strip():
                report(path, text, 'empty English/Chinese translation', match.start())
            if placeholders(*first[:2]) != placeholders(*second[:2]):
                report(path, text, f'placeholder mismatch: {first[0]!r} / {second[0]!r}', match.start())
    for message in ERRORS: print('ERROR ' + message)
    for message in WARNINGS: print('REVIEW ' + message)
    print(f'Checked {len(references)} XAML keys and {checked} literal translation pairs; '
          f'{skipped} dynamic/nonliteral calls skipped. '
          f'{len(ERRORS)} error(s), {len(WARNINGS)} review item(s).')
    return 1 if ERRORS else 0


if __name__ == '__main__':
    sys.exit(main())
