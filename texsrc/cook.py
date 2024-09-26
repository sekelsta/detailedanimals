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

def shade(shading, srcfile, outfile):
    image = Image.open(srcfile)
    pixels = image.load()
    sh = shading.load()
    for x in range(image.size[0]):
        for y in range(image.size[1]):
            r, g, b, a = pixels[x, y]
            s = sh[x, y]
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
    image.save(outfile)


source_dir = 'texsrc/chicken/'
out_dir = 'assets/detailedanimals/textures/entity/chicken/'


shading = Image.open(source_dir + 'shading.png').convert('L')
mass_convert(lambda src, out: shade(shading, src, out), source_dir + 'unshaded/', out_dir)
