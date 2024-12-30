import math

vitality_genes = 16

# If X is the chance for each gene to be homozygous deleterious, this returns the chance the animal will be nonviable
def fourhits(x):
    result = 0
    for i in range(4, vitality_genes + 1):
        result += x ** i * (1 - x) ** (vitality_genes - i) * math.comb(vitality_genes, i)
    return result

def infertility(COI, resistance):
    deleterious = 255 / 256 * (1 - resistance)
    base = deleterious * deleterious / 255
    x = COI * deleterious + (1 - COI) * base
    return fourhits(x)

def perfect(COI, resistance):
    deleterious = 255 / 256 * (1 - resistance)
    base = deleterious * deleterious / 255
    x = COI * deleterious + (1 - COI) * base
    return (1 - x) ** vitality_genes

def fertility(COI, resistance):
    return 1 - infertility(COI, resistance)
