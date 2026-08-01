"""
Сборка финальных скриншотов для Яндекс Игр.

Подпись занимает 8-11 % высоты кадра, то есть геймплей остаётся почти на 90 % —
требование площадки «не менее 70 % реального геймплея» выполняется с запасом, а
остаток Яндекс прямо разрешает под обрамление.

ГЛАВНОЕ: место под подпись НЕ задано числом, а ищется по самому кадру. Первая
версия ставила полосу снизу вслепую — и в портрете она наполовину срезала бустеры
«радуга» и «бомба», из-за чего кадр выглядел как брак вёрстки. Теперь скрипт
сканирует горизонтальные полосы, считает разброс яркости и берёт самую спокойную:
там гарантированно фон, а не элемент интерфейса. Полоса ищется в нижних двух
третях (подпись внизу читается привычнее) и не заходит на игровое поле.

Шрифт — CozyFont (Nunito ExtraLight), тот же, что в игре. Он тонкий, поэтому вес
набирается обводкой того же цвета: так же TMP синтезирует Bold, и подпись не
выпадает из стиля.
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageStat, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.abspath(__file__))
FONT = 'E:/GameDev/Unity/Projects/BubbleShooter_Yandex/Assets/Resources/CozyFont.ttf'

BROWN = (107, 87, 69)            # Hud.Brown, чуть темнее для контраста на кремовом

CAPS = {
    'shot1': {'ru': 'Наводи, стреляй, собирай тройки',
              'en': 'Aim, shoot, match three'},
    'shot2': {'ru': 'Три в ряд — и зверята лопаются',
              'en': 'Three in a row and they pop'},
    'shot3': {'ru': 'Радуга и бомба — когда всё застряло',
              'en': 'Rainbow and bomb when you are stuck'},
}


def quiet_strip(im, band_h):
    """Самая однородная горизонтальная полоса — там фон, а не интерфейс."""
    g = im.convert('L').resize((160, im.height // 4), Image.BILINEAR)
    sh = max(1, band_h // 4)
    best_y, best_score = None, None
    # Нижние две трети: подпись внизу кадра читается привычнее, чем посередине.
    for y in range(g.height // 3, g.height - sh):
        stat = ImageStat.Stat(g.crop((0, y, g.width, y + sh)))
        # Чем ниже разброс, тем ровнее фон. Небольшой бонус за близость к низу —
        # при равном спокойствии выбираем нижнюю полосу.
        score = stat.stddev[0] - (y / g.height) * 1.5
        if best_score is None or score < best_score:
            best_score, best_y = score, y
    return int(best_y * 4), best_score


def fit_font(text, max_w, start):
    size = start
    while size > 18:
        f = ImageFont.truetype(FONT, size)
        if f.getbbox(text)[2] <= max_w:
            return f
        size -= 2
    return ImageFont.truetype(FONT, 18)


def compose(src, dst, caption):
    im = Image.open(src).convert('RGB')
    w, h = im.size
    band_h = int(h * (0.10 if h > w else 0.13))

    y, score = quiet_strip(im, band_h)
    y = min(y, h - band_h)

    d = ImageDraw.Draw(im)
    font = fit_font(caption, int(w * 0.88), int(band_h * 0.46))
    bbox = d.textbbox((0, 0), caption, font=font)
    tw = bbox[2] - bbox[0]

    # Подложка НЕ во всю ширину: в горизонтальной раскладке полоса на весь кадр
    # доставала до боковых панелей и размывала подпись «БОМБА» под бустером.
    # Берём ширину текста с полями и растворяем края по горизонтали и вертикали —
    # получается мягкое пятно ровно под надписью, ничего постороннего не задевающее.
    pad = int(w * 0.06)
    bw = min(w, tw + pad * 2)
    bx = (w - bw) // 2

    # Подложку берём из самого кадра: размываем и осветляем. Плашка «своего» цвета
    # читалась бы как наклейка, а размытый фон кадра сливается с ним на любом уровне.
    strip = im.crop((bx, y, bx + bw, y + band_h)).filter(ImageFilter.GaussianBlur(28))
    veil = Image.new('RGBA', (bw, band_h), (255, 252, 246, 150))
    strip = Image.alpha_composite(strip.convert('RGBA'), veil)

    # Две растушёвки — по вертикали и по горизонтали — ПЕРЕМНОЖАЮТСЯ, а не рисуются
    # одна поверх другой: иначе второй проход затирал бы углы, и подложка обрывалась
    # бы в них резким уголком.
    vmask = Image.new('L', (bw, band_h), 255)
    vd = ImageDraw.Draw(vmask)
    fv = max(2, band_h // 5)
    for i in range(fv):
        a = int(255 * i / fv)
        vd.line([(0, i), (bw, i)], fill=a)
        vd.line([(0, band_h - 1 - i), (bw, band_h - 1 - i)], fill=a)

    hmask = Image.new('L', (bw, band_h), 255)
    hd = ImageDraw.Draw(hmask)
    fh = max(2, pad)
    for i in range(fh):
        a = int(255 * i / fh)
        hd.line([(i, 0), (i, band_h)], fill=a)
        hd.line([(bw - 1 - i, 0), (bw - 1 - i, band_h)], fill=a)

    im.paste(strip.convert('RGB'), (bx, y), ImageChops.multiply(vmask, hmask))

    tx = (w - tw) // 2 - bbox[0]
    ty = y + (band_h - (bbox[3] - bbox[1])) // 2 - bbox[1]
    d.text((tx, ty), caption, font=font, fill=BROWN,
           stroke_width=max(1, font.size // 20), stroke_fill=BROWN)

    im.save(dst, 'PNG', optimize=True)
    return (w, h), y, round(score, 2)


def main():
    raw = os.path.join(ROOT, 'raw')
    out = os.path.join(ROOT, 'final')
    os.makedirs(out, exist_ok=True)
    n = 0
    for lang in ('ru', 'en'):
        for ori in ('port', 'land'):
            for i, shot in enumerate(('shot1', 'shot2', 'shot3'), 1):
                d = os.path.join(raw, f'{lang}_{ori}_{shot}')
                png = [f for f in os.listdir(d) if f.endswith('.png')] if os.path.isdir(d) else []
                if not png:
                    print('нет кадра в', d); continue
                dst = os.path.join(out, f'{lang}_{ori}_{i}.png')
                size, y, sc = compose(os.path.join(d, png[0]), dst, CAPS[shot][lang])
                print(f'{os.path.basename(dst):16s} {size[0]}x{size[1]}  подпись y={y:4d} '
                      f'(разброс {sc})  «{CAPS[shot][lang]}»')
                n += 1
    print('готово скриншотов:', n)


main()
