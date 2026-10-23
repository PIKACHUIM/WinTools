"""把 WOFF2 字体转换为 WPF 可用的 TTF，并打印字体族名。

WPF 的字体系统只支持 TrueType/OpenType（.ttf/.otf），不支持 WOFF2，
所以内置的 Web 字体需要先解包。

用法：py tools/Convert-Woff2ToTtf.py <输入.woff2> <输出.ttf>
依赖：py -m pip install fonttools brotli
"""

import sys
from fontTools.ttLib import TTFont

# WOFF2/WEB 专用表，桌面渲染无意义
WEB_TABLES = ("TSI0", "TSI1", "TSI2", "TSI3", "TSI5")


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    source, target = sys.argv[1], sys.argv[2]
    font = TTFont(source)
    font.flavor = None

    for tag in WEB_TABLES:
        if tag in font:
            del font[tag]

    font.save(target)

    names = {}
    for record in font["name"].names:
        if record.nameID in (1, 4, 6, 16, 17) and record.platformID == 3:
            try:
                names.setdefault(record.nameID, record.toUnicode())
            except Exception:
                pass

    print(f"saved: {target}")
    print(f"family(1): {names.get(1)}")
    print(f"full(4):   {names.get(4)}")
    print(f"ps(6):     {names.get(6)}")
    print(f"typo(16):  {names.get(16)}")
    print(f"sub(17):   {names.get(17)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
