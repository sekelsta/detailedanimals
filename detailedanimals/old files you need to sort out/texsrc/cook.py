import os
from PIL import Image


def mkdir(dirname):
    if not os.path.isdir(dirname):
        os.mkdir(dirname)

def mass_convert(func, srcdir, enddir):
    mkdir(enddir)
    for name in os.listdir(srcdir):
        srcname = srcdir + '/' + name
        endname = enddir + '/' + name
        if os.path.isdir(srcname):
            mass_convert(func, srcname, endname)
        else:
            func(srcname, endname)

def shade(pixels, shading_pixels, width, height):
    for x in range(width):
        for y in range(height):
            r, g, b, a = pixels[x, y]
            s = shading_pixels[x, y]
            fr = (r/255.0) ** 2.2
            fg = (g/255.0) ** 2.2
            fb = (b/255.0) ** 2.2
            fs = (s/255.0) ** 2.2
            highlight = 0.04
            fr = fr * (1 - highlight) + fs * highlight
            fg = fg * (1 - highlight) + fs * highlight
            fb = fb * (1 - highlight) + fs * highlight
            darkness = 0.8
            fr = fr * (1 - darkness) + (fr * fs) * darkness
            fg = fg * (1 - darkness) + (fg * fs) * darkness
            fb = fb * (1 - darkness) + (fb * fs) * darkness
            pixels[x, y] = (round(fr ** (1/2.2) * 255), round(fg ** (1/2.2) * 255), round(fb ** (1/2.2) * 255), a)

def blend_RGBA(base, layer, out, width, height):
    for x in range(width):
        for y in range(height):
            r, g, b, a = base[x, y]
            lr, lg, lb, la = layer[x, y]
            la = la / 255.0
            # Assume not premultiplied
            fr = r * (1 - la) + lr * la
            fg = g * (1 - la) + lg * la
            fb = b * (1 - la) + lb * la
            fa = a + la * (255 - a)
            out[x, y] = (round(fr), round(fg), round(fb), round(fa))

def mask(image, mask, width, height):
    for x in range(width):
        for y in range(height):
            r, g, b, a = image[x, y]
            ma = mask[x, y]
            image[x, y] = (r, g, b, round(a * ma / 255.0))



def convert(ground, pattern, common, shading, alpha, srcfile, outfile):
    image = Image.open(srcfile)
    pixels = image.load()
    width = image.size[0]
    height = image.size[1]
    if pattern:
        mask(pixels, pattern, width, height)
    if ground:
        blend_RGBA(ground, pixels, pixels, width, height)
    blend_RGBA(pixels, common, pixels, width, height)
    shade(pixels, shading, width, height)
    mask(pixels, alpha, width, height)
    image.save(outfile)

source_dir = 'texsrc/chicken/'
out_dir = 'assets/detailedanimals/textures/entity/chicken/'


shading = Image.open(source_dir + 'adult-shading.png').convert('L')
shading_pixels = shading.load()
alpha = Image.open(source_dir + 'adult-alpha.png').convert('L')
alpha_pixels = alpha.load()
henground = Image.open(source_dir + 'color/hen-ground.png')
henground_pixels = henground.load()
roosterground = Image.open(source_dir + 'color/rooster-ground.png')
roosterground_pixels = roosterground.load()
common = Image.open(source_dir + 'adult-common.png')
common_pixels = common.load()

patterns = ["hen-birchen", "rooster-birchen", "hen-duckwing", "rooster-duckwing"]
colors = ["adult-black", "hen-blue", "rooster-blue", "hen-splash", "rooster-splash", "adult-white"]

# Self pattern
for color in colors:
    convert(None, None, common_pixels, shading_pixels, alpha_pixels, source_dir + "/color/" + color + ".png", out_dir + color + ".png")
for pattern in patterns:
    is_hen = pattern.startswith('hen-')
    is_rooster = pattern.startswith('rooster-')
    pattern_pixels = Image.open(source_dir + 'pattern/' + pattern + '.png').convert('L').load()
    for color in colors:
        if color == 'adult-white':
            # Dominant white not in yet
            continue
        if (is_hen and color.startswith('rooster-')) or (is_rooster and color.startswith('hen-')):
            continue
        color_name = color.removeprefix('hen-').removeprefix('rooster-').removeprefix('adult-')
        if color_name == 'black':
            color_name = ''
        else:
            color_name = '-' + color_name
        print(pattern, color, pattern + color_name)
        ground = henground_pixels if is_hen else roosterground_pixels
        convert(ground, pattern_pixels, common_pixels, shading_pixels, alpha_pixels, source_dir + "color/" + color + ".png", out_dir + pattern + color_name + ".png")

