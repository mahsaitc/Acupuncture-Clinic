"""The point library and where each point sits on every chart it appears on.

Symmetric charts (front, back, head-front) take (dx, y): dx is the distance from the midline, 0 for a
midline point. Every other chart takes (x, y) in its own units. Positions follow cun proportions
measured on the drawings (see the landmark notes below); they are a recording guide, not an atlas.
"""

# Front: suprasternal notch 89, xiphoid 144, navel 196, pubis 238; abdomen ~8.4 px/cun below the
# navel, ~7.1 above; ~4.5 px/cun across. Elbow crease 189, wrist 262.5 (6.2 px/cun on the forearm).
# Knee (ST35) 344, lateral malleolus 441 (6.06 px/cun on the lower leg).
NAVEL, XIPHOID, PUBIS = 196, 139, 238


def above_navel(cun):
    return round(NAVEL - cun * (NAVEL - XIPHOID) / 8, 1)


def below_navel(cun):
    return round(NAVEL + cun * (PUBIS - NAVEL) / 5, 1)


# Back: spinous processes. C7 86, T1..T12 evenly to 171, then the lumbar vertebrae and sacral foramina.
SPINE = {"C7": 86}
for i in range(1, 13):
    SPINE[f"T{i}"] = 91 + (i - 1) * 80 / 11
SPINE.update({"L1": 179, "L2": 187.5, "L3": 196, "L4": 204.5, "L5": 213, "S1": 219, "S2": 226, "S3": 232.5, "S4": 238.5})
ORDER = list(SPINE)


def below(level):
    """The depression below a spinous process, where GV points and the back-shu points sit."""
    if level.startswith("S"):
        return round(SPINE[level], 1)
    nxt = ORDER[ORDER.index(level) + 1]
    return round((SPINE[level] + SPINE[nxt]) / 2, 1)


INNER, OUTER = 7.5, 15  # the two Bladder lines, 1.5 and 3 cun from the spine

# Arm charts (inner: palm towards us, thumb on the right). Elbow crease 230, wrist 400: 14.2 px/cun.
WRIST, ELBOW = 400, 230


def arm_up(cun):
    return round(WRIST - cun * (WRIST - ELBOW) / 12, 1)


def outer(x):
    """The outer arm and leg charts are mirror images of the inner ones."""
    return round(160 - x, 1)


# Leg charts: knee joint line 238, malleolus 422 (11.5 px/cun below the knee).
def leg_down(cun):
    return round(244 + cun * 11.5, 1)


VIEWS = [
    # key, page, symmetric, faces the viewer (patient's right on the viewer's left)
    ("front", "body", True, True),
    ("back", "body", True, False),
    ("side", "body", False, False),
    ("head-front", "head", True, True),
    ("head-side", "head", False, False),
    ("arm-inner", "arm", False, False),
    ("arm-outer", "arm", False, False),
    ("leg-inner", "leg", False, False),
    ("leg-outer", "leg", False, False),
    ("ear", "ear", False, False),
]

POINTS = []


def P(code, name, meridian, **views):
    POINTS.append((code, name, meridian, {k.replace("_", "-"): v for k, v in views.items()}))


# ------------------------------------------------------------------ Lung
P("LU1", "Zhongfu", "LU", front=(29, 101))
P("LU5", "Chize", "LU", front=(58.5, 189), arm_inner=(94, ELBOW))
P("LU6", "Kongzui", "LU", front=(66.5, 220), arm_inner=(93.5, arm_up(7)))
P("LU7", "Lieque", "LU", front=(71.5, 253.5), arm_inner=(95.5, arm_up(1.5)))
P("LU9", "Taiyuan", "LU", front=(72.5, 262.5), arm_inner=(93, WRIST))
P("LU10", "Yuji", "LU", front=(74, 272), arm_inner=(101, 421))
P("LU11", "Shaoshang", "LU", front=(80.5, 283), arm_inner=(117, 441))

# ------------------------------------------------------------------ Large intestine
P("LI1", "Shangyang", "LI", arm_outer=(outer(94.5), 464))
P("LI4", "Hegu", "LI", back=(72, 278), arm_outer=(outer(99), 425))
P("LI5", "Yangxi", "LI", back=(73, 262.5), arm_outer=(outer(96.5), 401))
P("LI6", "Pianli", "LI", back=(71, 244), arm_outer=(outer(98), arm_up(3)))
P("LI10", "Shousanli", "LI", back=(64, 200.5), arm_outer=(outer(102.5), 258))
P("LI11", "Quchi", "LI", front=(63, 188), back=(63, 188), arm_outer=(outer(104.5), 229), arm_inner=(104, 229))
P("LI14", "Binao", "LI", front=(51.5, 140), arm_outer=(outer(87), 165))
P("LI15", "Jianyu", "LI", front=(45.5, 101), arm_outer=(outer(91), 26))
P("LI20", "Yingxiang", "LI", front=(3.6, 50), head_front=(18, 158), head_side=(184, 152))

# ------------------------------------------------------------------ Stomach
P("ST1", "Chengqi", "ST", head_front=(29.5, 133))
P("ST2", "Sibai", "ST", head_front=(29.5, 141))
P("ST3", "Juliao", "ST", head_front=(29.5, 158))
P("ST4", "Dicang", "ST", front=(5.5, 58), head_front=(27, 194))
P("ST6", "Jiache", "ST", front=(14, 59.5), head_front=(43, 207), head_side=(141, 186))
P("ST7", "Xiaguan", "ST", head_front=(64, 142), head_side=(142, 137))
P("ST8", "Touwei", "ST", head_front=(55, 62), head_side=(158, 50))
P("ST21", "Liangmen", "ST", front=(9, above_navel(4)))
P("ST24", "Huaroumen", "ST", front=(9, above_navel(1)))
P("ST25", "Tianshu", "ST", front=(9, NAVEL))
P("ST26", "Wailing", "ST", front=(9, below_navel(1)))
P("ST28", "Shuidao", "ST", front=(9, below_navel(3)))
P("ST29", "Guilai", "ST", front=(9, below_navel(4)))
P("ST32", "Futu", "ST", front=(25, 295), leg_outer=(outer(98), 150))
P("ST34", "Liangqiu", "ST", front=(24, 317), leg_outer=(outer(94), 184))
P("ST35", "Dubi", "ST", front=(21.5, 344), leg_outer=(outer(92), 244))
P("ST36", "Zusanli", "ST", front=(21.5, 362), side=(108.5, 366), leg_outer=(outer(82), leg_down(3)))
P("ST37", "Shangjuxu", "ST", front=(22, 380), leg_outer=(outer(80.5), leg_down(6)))
P("ST39", "Xiajuxu", "ST", front=(22.5, 398.5), leg_outer=(outer(79), leg_down(9)))
P("ST40", "Fenglong", "ST", front=(26, 392.5), side=(104.5, 396), leg_outer=(outer(72.5), leg_down(8)))
P("ST41", "Jiexi", "ST", front=(15.5, 442), leg_outer=(outer(84.5), 425))
P("ST44", "Neiting", "ST", front=(14.6, 462), leg_outer=(outer(129), 452))

# ------------------------------------------------------------------ Spleen
P("SP1", "Yinbai", "SP", leg_inner=(140, 454))
P("SP3", "Taibai", "SP", leg_inner=(122, 457))
P("SP4", "Gongsun", "SP", leg_inner=(104, 455))
P("SP5", "Shangqiu", "SP", leg_inner=(80, 432))
P("SP6", "Sanyinjiao", "SP", front=(8.5, 420), leg_inner=(73, 422 - 3 * 13.5))
P("SP8", "Diji", "SP", front=(7.5, 368), leg_inner=(74, 252 + 3 * 13.5))
P("SP9", "Yinlingquan", "SP", front=(7, 350), leg_inner=(74, 252))
P("SP10", "Xuehai", "SP", front=(8, 317), leg_inner=(85, 179))
P("SP15", "Daheng", "SP", front=(18, NAVEL))
P("SP21", "Dabao", "SP", side=(103, 140))

# ------------------------------------------------------------------ Heart
P("HT3", "Shaohai", "HT", front=(46, 189), arm_inner=(58, ELBOW))
P("HT5", "Tongli", "HT", front=(59, 256.5), arm_inner=(68, arm_up(1)))
P("HT6", "Yinxi", "HT", arm_inner=(68.2, arm_up(0.5)))
P("HT7", "Shenmen", "HT", front=(59.5, 262.5), arm_inner=(68.5, WRIST))
P("HT9", "Shaochong", "HT", arm_inner=(70.6, 465))

# ------------------------------------------------------------------ Small intestine
P("SI1", "Shaoze", "SI", arm_outer=(outer(66.6), 464))
P("SI3", "Houxi", "SI", back=(58, 290), arm_outer=(outer(66), 440))
P("SI6", "Yanglao", "SI", back=(59.5, 256), arm_outer=(outer(68), arm_up(1)))
P("SI8", "Xiaohai", "SI", back=(51, 183), arm_outer=(outer(66), 222))
P("SI9", "Jianzhen", "SI", back=(38, 131))
P("SI11", "Tianzong", "SI", back=(29, 121))
P("SI12", "Bingfeng", "SI", back=(29, 99))
P("SI18", "Quanliao", "SI", head_front=(46, 161))
P("SI19", "Tinggong", "SI", head_side=(131.5, 134))

# ------------------------------------------------------------------ Bladder
P("BL1", "Jingming", "BL", head_front=(13.5, 118))
P("BL2", "Zanzhu", "BL", head_front=(13, 102), front=(3, 34))
P("BL10", "Tianzhu", "BL", back=(4.5, 61.5), head_side=(66, 196))
for code, name, level, line in [
    ("BL11", "Dazhu", "T1", INNER), ("BL12", "Fengmen", "T2", INNER), ("BL13", "Feishu", "T3", INNER),
    ("BL14", "Jueyinshu", "T4", INNER), ("BL15", "Xinshu", "T5", INNER), ("BL17", "Geshu", "T7", INNER),
    ("BL18", "Ganshu", "T9", INNER), ("BL19", "Danshu", "T10", INNER), ("BL20", "Pishu", "T11", INNER),
    ("BL21", "Weishu", "T12", INNER), ("BL22", "Sanjiaoshu", "L1", INNER), ("BL23", "Shenshu", "L2", INNER),
    ("BL25", "Dachangshu", "L4", INNER), ("BL27", "Xiaochangshu", "S1", INNER), ("BL28", "Pangguangshu", "S2", INNER),
    ("BL43", "Gaohuang", "T4", OUTER), ("BL52", "Zhishi", "L2", OUTER), ("BL54", "Zhibian", "S4", OUTER),
]:
    P(code, name, "BL", back=(line, below(level)))
P("BL32", "Ciliao", "BL", back=(4, SPINE["S2"]))
P("BL36", "Chengfu", "BL", back=(17, 265))
P("BL37", "Yinmen", "BL", back=(17.5, 300))
P("BL40", "Weizhong", "BL", back=(17.5, 343), leg_outer=(outer(50), 232))
P("BL57", "Chengshan", "BL", back=(17.5, 395))
P("BL58", "Feiyang", "BL", back=(21, 406))
P("BL60", "Kunlun", "BL", back=(21.5, 438), side=(94.5, 442), leg_outer=(outer(60.5), 422))
P("BL62", "Shenmai", "BL", side=(100, 447.5), leg_outer=(outer(70), 433))
P("BL67", "Zhiyin", "BL", leg_outer=(outer(141), 460))

# ------------------------------------------------------------------ Kidney
P("KI1", "Yongquan", "KI", leg_inner=(113, 465))
P("KI3", "Taixi", "KI", back=(9, 437), leg_inner=(63, 423))
P("KI6", "Zhaohai", "KI", leg_inner=(70, 434.5))
P("KI7", "Fuliu", "KI", back=(10, 425), leg_inner=(61.5, 396))
P("KI10", "Yingu", "KI", back=(9.5, 343), leg_inner=(49.5, 233))
P("KI27", "Shufu", "KI", front=(9, 91))

# ------------------------------------------------------------------ Pericardium
P("PC3", "Quze", "PC", front=(54, 190), arm_inner=(84, ELBOW + 1))
P("PC4", "Ximen", "PC", front=(63.5, 231), arm_inner=(80.5, arm_up(5)))
P("PC5", "Jianshi", "PC", front=(64.5, 243.5), arm_inner=(80.5, arm_up(3)))
P("PC6", "Neiguan", "PC", front=(65, 250), arm_inner=(80.5, arm_up(2)))
P("PC7", "Daling", "PC", front=(66, 263), arm_inner=(80.5, WRIST))
P("PC8", "Laogong", "PC", front=(67, 282), arm_inner=(79, 428))
P("PC9", "Zhongchong", "PC", arm_inner=(83.2, 475))

# ------------------------------------------------------------------ San jiao (triple energiser)
P("SJ1", "Guanchong", "SJ", arm_outer=(outer(74.4), 472))
P("SJ3", "Zhongzhu", "SJ", back=(62, 286), arm_outer=(outer(71), 436))
P("SJ4", "Yangchi", "SJ", back=(66, 262.5), arm_outer=(outer(80.5), WRIST))
P("SJ5", "Waiguan", "SJ", back=(65.5, 250), arm_outer=(outer(80.5), arm_up(2)))
P("SJ6", "Zhigou", "SJ", back=(64.5, 243.5), arm_outer=(outer(80.5), arm_up(3)))
P("SJ10", "Tianjing", "SJ", back=(55, 172), arm_outer=(outer(80), 199))
P("SJ14", "Jianliao", "SJ", back=(46, 101), arm_outer=(outer(70), 26))
P("SJ17", "Yifeng", "SJ", head_side=(108, 171))
P("SJ21", "Ermen", "SJ", head_side=(130, 124))
P("SJ23", "Sizhukong", "SJ", head_front=(50, 103))

# ------------------------------------------------------------------ Gall bladder
P("GB1", "Tongziliao", "GB", head_front=(48, 121), head_side=(165.5, 106))
P("GB2", "Tinghui", "GB", head_side=(130, 146))
P("GB8", "Shuaigu", "GB", head_side=(112, 80))
P("GB12", "Wangu", "GB", head_side=(98, 177))
P("GB14", "Yangbai", "GB", front=(7.5, 29), head_front=(29.5, 88))
P("GB20", "Fengchi", "GB", back=(9, 56), head_side=(77, 183))
P("GB21", "Jianjing", "GB", back=(22, 91), front=(22, 90.5))
P("GB25", "Jingmen", "GB", side=(96, 186))
P("GB26", "Daimai", "GB", side=(106, NAVEL))
P("GB30", "Huantiao", "GB", back=(24.5, 242), side=(86, 238))
P("GB31", "Fengshi", "GB", side=(104, 304), leg_outer=(outer(66), 165))
P("GB34", "Yanglingquan", "GB", front=(28, 351), side=(100.5, 358), leg_outer=(outer(64), 256))
P("GB39", "Xuanzhong", "GB", front=(25, 422), side=(101, 422), leg_outer=(outer(68.5), 386))
P("GB40", "Qiuxu", "GB", front=(22.5, 447), side=(104.5, 446), leg_outer=(outer(79), 431))
P("GB41", "Zulinqi", "GB", front=(20, 456), leg_outer=(outer(110), 449))

# ------------------------------------------------------------------ Liver
P("LR2", "Xingjian", "LR", front=(10.4, 462))
P("LR3", "Taichong", "LR", front=(10.5, 454), leg_inner=(120, 445))
P("LR5", "Ligou", "LR", front=(6.5, 410), leg_inner=(79, 422 - 5 * 13.5))
P("LR8", "Ququan", "LR", leg_inner=(57.5, 233))
P("LR13", "Zhangmen", "LR", front=(28, 178), side=(108, 178))
P("LR14", "Qimen", "LR", front=(18, 146))

# ------------------------------------------------------------------ Governing vessel
P("GV1", "Changqiang", "GV", back=(0, 251))
P("GV2", "Yaoshu", "GV", back=(0, 243))
P("GV3", "Yaoyangguan", "GV", back=(0, below("L4")))
P("GV4", "Mingmen", "GV", back=(0, below("L2")))
P("GV9", "Zhiyang", "GV", back=(0, below("T7")))
P("GV12", "Shenzhu", "GV", back=(0, below("T3")))
P("GV14", "Dazhui", "GV", back=(0, below("C7")))
P("GV15", "Yamen", "GV", back=(0, 59.5), head_side=(56.5, 197))
P("GV16", "Fengfu", "GV", back=(0, 55), head_side=(55, 184))
P("GV20", "Baihui", "GV", front=(0, 14), back=(0, 14), head_front=(0, 22), head_side=(112, 19))
P("GV24", "Shenting", "GV", head_front=(0, 50), head_side=(178, 52))
P("GV26", "Shuigou", "GV", front=(0, 53.6), head_front=(0, 172))

# ------------------------------------------------------------------ Conception vessel
P("CV2", "Qugu", "CV", front=(0, PUBIS))
P("CV3", "Zhongji", "CV", front=(0, below_navel(4)))
P("CV4", "Guanyuan", "CV", front=(0, below_navel(3)))
P("CV6", "Qihai", "CV", front=(0, below_navel(1.5)))
P("CV8", "Shenque", "CV", front=(0, NAVEL))
P("CV9", "Shuifen", "CV", front=(0, above_navel(1)))
P("CV10", "Xiawan", "CV", front=(0, above_navel(2)))
P("CV12", "Zhongwan", "CV", front=(0, above_navel(4)))
P("CV13", "Shangwan", "CV", front=(0, above_navel(5)))
P("CV14", "Juque", "CV", front=(0, above_navel(6)))
P("CV17", "Danzhong", "CV", front=(0, 128))
P("CV22", "Tiantu", "CV", front=(0, 89))
P("CV23", "Lianquan", "CV", front=(0, 74))
P("CV24", "Chengjiang", "CV", front=(0, 63), head_front=(0, 212))

# ------------------------------------------------------------------ Extra points
P("EX-HN1", "Sishencong", "EX", head_side=(100, 22))
P("EX-HN3", "Yintang", "EX", front=(0, 34.5), head_front=(0, 104))
P("EX-HN4", "Yuyao", "EX", head_front=(29, 100))
P("EX-HN5", "Taiyang", "EX", front=(16, 36.5), head_front=(56, 112), head_side=(161, 104))
P("EX-B2", "Jiaji", "EX", back=(2.5, below("L1")))
P("EX-LE4", "Neixiyan", "EX", front=(13, 344))
P("EX-UE9", "Baxie", "EX", back=(64, 296))
P("EX-LE10", "Bafeng", "EX", front=(18, 463))

# ------------------------------------------------------------------ Ear (auricular), on the ear chart only
for code, name, x, y in [
    ("HX1", "Ear center", 78, 120), ("HX6,7i", "Ear apex", 118, 26),
    ("TF4", "Shenmen", 112, 70), ("AH6a", "Sympathetic", 84, 90),
    ("CO1", "Mouth", 82, 133), ("CO2", "Esophagus", 94, 132), ("CO3", "Cardia", 106, 133),
    ("CO4", "Stomach", 119, 136), ("CO5", "Duodenum", 118, 113), ("CO6", "Small intestine", 103, 110),
    ("CO7", "Large intestine", 89, 107), ("CO10", "Kidney", 117, 102), ("CO12", "Liver", 132, 140),
    ("CO13", "Spleen", 130, 160), ("CO14", "Lung", 95, 176), ("CO15", "Heart", 109, 162),
    ("CO17", "San jiao", 104, 195), ("CO18", "Endocrine", 92, 207), ("AT4", "Subcortex", 117, 220),
    ("TG", "Hunger", 70, 176), ("LO5", "Eye", 108, 257),
]:
    P(code, name, "EAR", ear=(x, y))

PROTOCOLS = [
    ("weight", "Weight loss", ["CV12", "ST25", "SP15", "CV6", "ST28", "ST36", "ST40", "SP6"]),
    ("weight-ear", "Weight loss, ear", ["TF4", "CO4", "CO13", "CO18", "TG", "HX1"]),
    ("headache", "Headache", ["GV20", "EX-HN5", "GB20", "LI4", "LR3"]),
    ("back", "Low back pain", ["BL23", "BL25", "GV4", "GB30", "BL40", "BL57"]),
    ("neck", "Neck and shoulder pain", ["GB20", "GB21", "SI3", "SJ5", "LI11", "BL10"]),
    ("sleep", "Insomnia and anxiety", ["GV20", "EX-HN3", "HT7", "PC6", "SP6"]),
    ("digestion", "Digestion", ["CV12", "ST25", "PC6", "ST36", "SP6"]),
    ("knee", "Knee pain", ["ST35", "EX-LE4", "SP9", "GB34", "ST36", "BL40"]),
]
