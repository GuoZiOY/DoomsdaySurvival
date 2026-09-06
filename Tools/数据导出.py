# -*- coding: utf-8 -*-
"""游戏数据 Excel 导入导出工具（JSON <-> xlsx）

用法（在本目录下，PowerShell 执行）：
    py 数据导出.py to_xlsx    # 现有 JSON -> 游戏数据.xlsx（生成/刷新模板，供策划编辑）
    py 数据导出.py to_json    # 游戏数据.xlsx -> JSON（策划改完后导回 Assets/Resources/Data）
    py 数据导出.py init       # 等价 to_xlsx

依赖：openpyxl（pip install openpyxl）
约定：所有 xlsx 单元格文本一律 UTF-8；导出 JSON 用 UTF-8 无 BOM。
"""

import json
import os
import re
import sys

from openpyxl import Workbook, load_workbook

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.normpath(os.path.join(BASE_DIR, "..", "Assets", "Resources", "Data"))
XLSX_PATH = os.path.join(BASE_DIR, "游戏数据.xlsx")

# ============================================================
# 约定字符串：列表项之间用「全角分号 ；」分隔（策划手填内容几乎不出现全角分号，安全无歧义）
# ============================================================

def _材料_str(v):
    """[{'物品':'木板','数量':5}, ...] -> '木板 x 5；布料 x 3'"""
    if not v:
        return ""
    return "；".join("{} x {}".format(x.get("物品", ""), x.get("数量", 1)) for x in v)

def _材料_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        if " x " in 项:
            name, num = 项.rsplit(" x ", 1)
            out.append({"物品": name.strip(), "数量": int(num.strip())})
        else:
            out.append({"物品": 项, "数量": 1})
    return out

def _装备_str(v):
    """职业初始装备 [{'标识':'钢管','数量':1},...] -> '钢管 x 1；旧军服 x 1'（键名为「标识」）"""
    if not v:
        return ""
    return "；".join("{} x {}".format(x.get("标识", ""), x.get("数量", 1)) for x in v)

def _装备_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        if " x " in 项:
            name, num = 项.rsplit(" x ", 1)
            out.append({"标识": name.strip(), "数量": int(num.strip())})
        else:
            out.append({"标识": 项, "数量": 1})
    return out

def _效果_str(v):
    """天赋效果 [{'目标':'体质','数值':2},...] -> '体质 +2；力量 -1'"""
    if not v:
        return ""
    parts = []
    for x in v:
        num = x.get("数值", 0)
        sign = "+" if num >= 0 else "-"
        parts.append("{} {}{}".format(x.get("目标", ""), sign, abs(num)))
    return "；".join(parts)

def _效果_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        m = re.match(r"^(.+?)\s*([+\-])(\d+(?:\.\d+)?)$", 项)
        if m:
            target, sign, num = m.group(1).strip(), m.group(2), float(m.group(3))
            out.append({"目标": target, "数值": num if sign == "+" else -num})
        else:
            out.append({"目标": 项, "数值": 0})
    return out

def _一维数值_str(v):
    """[100,130,150] -> '100；130；150'（家具效果/保质期倍率）"""
    if not v:
        return ""
    return "；".join(str(x) for x in v)

def _一维数值_parse(s):
    if not s or not str(s).strip():
        return []
    return [float(x) for x in str(s).split("；") if x.strip()]

def _分区_str(v):
    """容器分区 [{'名称':'脏水','列':3,'行':1,'允许类型':'材料'},...] -> '脏水 3x3 材料；过滤 3x2 材料'"""
    if not v:
        return ""
    parts = []
    for x in v:
        parts.append("{} {}x{} {}".format(
            x.get("名称", ""), x.get("列", 3), x.get("行", 1), x.get("允许类型", "")).strip())
    return "；".join(parts)

def _分区_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        m = re.match(r"^(.+?)\s+(\d+)\s*x\s*(\d+)\s*(.*)$", 项)
        if m:
            out.append({"名称": m.group(1).strip(), "列": int(m.group(2)),
                        "行": int(m.group(3)), "允许类型": m.group(4).strip()})
        else:
            out.append({"名称": 项, "列": 3, "行": 3, "允许类型": ""})
    return out

def _搜索表_str(v):
    """搜索表 [{'物品标识':'木板','权重':3,'数量最小':2,'数量最大':5},...] -> '木板 3:2~5；管道胶带 3:2~5'"""
    if not v:
        return ""
    parts = []
    for x in v:
        parts.append("{} {}:{}~{}".format(
            x.get("物品标识", ""), x.get("权重", 1),
            x.get("数量最小", 1), x.get("数量最大", 1)))
    return "；".join(parts)

def _搜索表_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        m = re.match(r"^(.+?)\s+(\d+)\s*:\s*(\d+)\s*~\s*(\d+)$", 项)
        if m:
            out.append({"物品标识": m.group(1).strip(), "权重": int(m.group(2)),
                        "数量最小": int(m.group(3)), "数量最大": int(m.group(4))})
        else:
            out.append({"物品标识": 项, "权重": 1, "数量最小": 1, "数量最大": 1})
    return out

def _列表_str(v):
    """字符串列表 ['a','b'] -> 'a；b'（书籍配方池等）"""
    if not v:
        return ""
    return "；".join(str(x) for x in v)

def _列表_parse(s):
    if not s or not str(s).strip():
        return []
    return [x.strip() for x in str(s).split("；") if x.strip()]

def _升数字_arr_str(v):
    """升级数组（家具）：每项含材料/形状宽高/容器列行/容器分区，压缩成文本（仅供展示，导出时从 xlsx 的 升级1材料/升级2材料 等列还原）"""
    return ""

# ============================================================
# JSON 加载
# ============================================================

def _load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def _save_json(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    # 确保无 BOM
    with open(path, "rb") as f:
        raw = f.read()
    if raw.startswith(b"\xef\xbb\xbf"):
        with open(path, "wb") as f:
            f.write(raw[3:])

def _load_key(path, key):
    """加载根对象里的数组"""
    if not os.path.exists(path):
        return []
    data = _load_json(path)
    return data.get(key, []) if isinstance(data, dict) else []

# 加载所有物品（合并多文件，带 类型/种类）
def 加载物品():
    物品 = []
    物品根 = os.path.join(DATA_DIR, "物品")
    for 根, _dirs, files in os.walk(物品根):
        for fn in files:
            if not fn.endswith(".json"):
                continue
            arr = _load_key(os.path.join(根, fn), "物品")
            物品.extend(arr)
    return 物品

# ============================================================
# 表结构定义
#   (sheet名, [(excel列名, json字段, 解析器), ...])
# 解析器: str/int/float/bool/材料/效果/一维数值/分区/搜索表/列表
# ============================================================

物品通用列 = [
    ("标识", "标识", "str"),
    ("描述", "描述", "str"),
    ("品质", "品质", "str"),
    ("形状宽", "形状宽", "int"),
    ("形状高", "形状高", "int"),
    ("重量", "重量", "int"),
    ("堆叠上限", "堆叠上限", "int"),
    ("最大耐久", "最大耐久", "int"),
    ("价值", "价值", "int"),
]

TABLES = [
    {
        "sheet": "物品·武器",
        "json": os.path.join(DATA_DIR, "物品", "items_武器.json"), "key": "物品",
        "类型过滤": ["武器"],
        "列": [
            ("标识", "标识", "str"), ("描述", "描述", "str"), ("品质", "品质", "str"),
            ("槽位", "槽位", "str"), ("武器种类", "武器种类", "str"), ("攻击距离", "攻击距离", "int"),
            ("套装", "套装", "str"), ("攻击", "攻击加成", "int"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("重量", "重量", "int"), ("堆叠上限", "堆叠上限", "int"), ("最大耐久", "最大耐久", "int"), ("价值", "价值", "int"),
        ],
    },
    {
        "sheet": "物品·防具",
        "json": "", "key": "物品",
        "类型过滤": ["防具"],
        "列": [
            ("标识", "标识", "str"), ("描述", "描述", "str"), ("品质", "品质", "str"),
            ("槽位", "槽位", "str"), ("套装", "套装", "str"),
            ("防御", "防御加成", "int"), ("生命", "生命加成", "int"), ("负重", "负重加成", "int"), ("抗性", "抗性", "int"),
            ("是容器", "是容器", "bool"), ("容器列", "容器列", "int"), ("容器行", "容器行", "int"), ("容器形状", "容器形状", "json"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("重量", "重量", "int"), ("堆叠上限", "堆叠上限", "int"), ("最大耐久", "最大耐久", "int"), ("价值", "价值", "int"),
        ],
    },
    {
        "sheet": "物品·消耗",
        "json": "", "key": "物品",
        "类型过滤": ["饮食", "医疗"],
        "列": [
            ("类型", "类型", "str"), ("标识", "标识", "str"), ("描述", "描述", "str"), ("品质", "品质", "str"),
            ("种类", "种类", "str"), ("恢复量", "恢复量", "int"), ("恢复目标", "恢复目标", "str"),
            ("保质期", "保质期", "int"), ("战斗内使用", "战斗内使用", "bool"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("重量", "重量", "int"), ("堆叠上限", "堆叠上限", "int"), ("价值", "价值", "int"),
        ],
    },
    {
        "sheet": "物品·材料",
        "json": "", "key": "物品",
        "类型过滤": ["材料"],
        "列": [
            ("种类", "种类", "str"), ("标识", "标识", "str"), ("描述", "描述", "str"), ("品质", "品质", "str"),
            ("保质期", "保质期", "int"), ("生长时间", "生长时间", "int"), ("成熟产物", "成熟产物", "str"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("重量", "重量", "int"), ("堆叠上限", "堆叠上限", "int"), ("价值", "价值", "int"),
        ],
    },
    {
        "sheet": "物品·杂项",
        "json": "", "key": "物品",
        "类型过滤": ["弹药", "容器", "任务品", "技能书", "书籍"],
        "列": [
            ("类型", "类型", "str"), ("标识", "标识", "str"), ("描述", "描述", "str"), ("品质", "品质", "str"),
            ("攻击", "攻击加成", "int"), ("战斗内使用", "战斗内使用", "bool"),
            ("是容器", "是容器", "bool"), ("容器列", "容器列", "int"), ("容器行", "容器行", "int"), ("容器允许类型", "容器允许类型", "str"),
            ("书籍种类", "书籍种类", "str"), ("书籍级别", "书籍级别", "str"), ("阅读时间分", "阅读时间分", "int"),
            ("配方池", "配方池", "列表"), ("技能", "技能", "str"), ("熟练度点", "熟练度点", "int"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("重量", "重量", "int"), ("堆叠上限", "堆叠上限", "int"), ("最大耐久", "最大耐久", "int"), ("价值", "价值", "int"),
        ],
    },
    {
        "sheet": "配方",
        "json": [os.path.join(DATA_DIR, "recipes_工作台.json"), os.path.join(DATA_DIR, "recipes_灶台.json"), os.path.join(DATA_DIR, "recipes_医疗站.json")],
        "key": "配方", "类型过滤": None,
        "列": [
            ("标识", "标识", "str"), ("名称", "名称", "str"), ("描述", "描述", "str"), ("类型", "类型", "str"),
            ("产物", "产物", "str"), ("产物数量", "产物数量", "int"),
            ("材料", "材料", "材料"), ("解锁图纸", "解锁图纸", "str"), ("需习得", "需习得", "bool"),
            ("需要等级", "需要等级", "int"), ("品质", "品质", "str"),
            ("制作时间分", "制作时间分", "int"), ("消耗倍率", "消耗倍率", "float"), ("消耗精力", "消耗精力", "int"),
        ],
    },
    {
        "sheet": "家具",
        "json": os.path.join(DATA_DIR, "家具.json"), "key": "家具", "类型过滤": None,
        "列": [
            ("标识", "标识", "str"), ("名称", "名称", "str"), ("描述", "描述", "str"),
            ("解锁等级", "解锁等级", "int"), ("价格", "价格", "int"),
            ("材料", "材料", "材料"), ("最大等级", "最大等级", "int"), ("功能类型", "功能类型", "str"),
            ("形状宽", "形状宽", "int"), ("形状高", "形状高", "int"),
            ("升级", "升级", "json"), ("效果", "效果", "一维数值"),
            ("是容器", "是容器", "bool"), ("容器列", "容器列", "int"), ("容器行", "容器行", "int"),
            ("容器允许类型", "容器允许类型", "str"), ("容器允许种类", "容器允许种类", "str"),
            ("保质期倍率", "保质期倍率", "一维数值"), ("容器分区", "容器分区", "json"),
            ("净化间隔分", "净化间隔分", "int"), ("净化材料A", "净化材料A", "str"),
            ("净化材料B", "净化材料B", "str"), ("净化产物", "净化产物", "str"), ("净化产物数量", "净化产物数量", "int"),
        ],
    },
    {
        "sheet": "职业",
        "json": os.path.join(DATA_DIR, "职业.json"), "key": "职业", "类型过滤": None,
        "列": [
            ("标识", "标识", "str"), ("名称", "名称", "str"), ("描述", "描述", "str"),
            ("属性分布", "属性分布", "属性分布"), ("初始技能", "初始技能", "str"), ("职业天赋", "天赋", "str"),
            ("初始装备", "初始装备", "装备"), ("开局文本", "开局文本", "str"),
        ],
    },
    {
        "sheet": "天赋",
        "json": os.path.join(DATA_DIR, "天赋.json"), "key": "天赋", "类型过滤": None,
        "列": [
            ("标识", "标识", "str"), ("名称", "名称", "str"), ("描述", "描述", "str"),
            ("点数", "点数", "int"), ("效果", "效果", "效果"),
        ],
    },
    {
        "sheet": "情报",
        "json": os.path.join(DATA_DIR, "情报.json"), "key": "情报", "类型过滤": None,
        "列": [
            ("文本", "文本", "str"), ("等级", "等级", "int"),
        ],
    },
    {
        "sheet": "搜索容器",
        "json": os.path.join(DATA_DIR, "搜索_地图类型.json"), "key": "地图类型", "类型过滤": None,
        "列": [
            ("地图类型", "地图类型", "str"), ("房间", "房间", "str"),
            ("标识", "标识", "str"), ("名称", "名称", "str"), ("描述", "描述", "str"),
            ("容器列", "容器列", "int"), ("容器行", "容器行", "int"), ("容器允许类型", "容器允许类型", "str"),
            ("搜索时间", "搜索时间", "float"), ("搜索表", "搜索表", "搜索表"),
        ],
    },
]

# ============================================================
# 序列化：JSON 值 -> 单元格文本
# ============================================================

_SER = {
    "str": lambda v: "" if v is None else str(v),
    "int": lambda v: "" if v is None else ("" if str(v).strip() == "" else int(v)),
    "float": lambda v: "" if v is None else ("" if str(v).strip() == "" else float(v)),
    "bool": lambda v: "" if v is None else ("是" if v else "否"),
    "材料": _材料_str,
    "装备": _装备_str,
    "效果": _效果_str,
    "一维数值": _一维数值_str,
    "分区": _分区_str,
    "搜索表": _搜索表_str,
    "列表": _列表_str,
    "地图类型": lambda v: v.get("标识", "") if isinstance(v, dict) else str(v),
}

def _属性分布_str(v):
    """职业属性分布 [{'属性':'体质','点数':5},...] -> '体质 5；力量 4'"""
    if not v:
        return ""
    return "；".join("{} {}".format(x.get("属性", ""), x.get("点数", 0)) for x in v)

def _属性分布_parse(s):
    if not s or not str(s).strip():
        return []
    out = []
    for 项 in str(s).split("；"):
        项 = 项.strip()
        if not 项:
            continue
        m = re.match(r"^(.+?)\s+(\d+)$", 项)
        out.append({"属性": m.group(1).strip(), "点数": int(m.group(2))} if m else {"属性": 项, "点数": 0})
    return out

def _升级材料_str(v):
    """家具升级数组 -> 展示用文本（多级材料合并，标明 1→2 级、2→3 级）"""
    if not v:
        return ""
    parts = []
    for i, 升级 in enumerate(v):
        材料 = 升级.get("材料") if 升级 else None
        parts.append("{}→{}级:{}".format(i + 1, i + 2, _材料_str(材料)))
    return "；".join(parts)

# 事后补充（规避定义顺序问题）
_SER["属性分布"] = _属性分布_str
_SER["升级材料"] = _升级材料_str
_SER["json"] = lambda v: "" if v is None else json.dumps(v, ensure_ascii=False)   # 复杂嵌套（升级/容器分区）用 JSON 原文，保证往返不丢字段

# ============================================================
# 反序列化：单元格文本 -> JSON 值
# ============================================================

def _parse_bool(s):
    if s is None:
        return False
    return str(s).strip() in ("是", "true", "True", "1", "1.0")

_DESER_FOR_KEY = {
    "材料": _材料_parse,
    "装备": _装备_parse,
    "效果": _效果_parse,
    "一维数值": _一维数值_parse,
    "分区": _分区_parse,
    "搜索表": _搜索表_parse,
    "列表": _列表_parse,
    "属性分布": _属性分布_parse,
    "升级材料": None,  # 家具升级按 多列 拆分，此处不用
    "json": lambda s: json.loads(s) if s and str(s).strip() else [],   # JSON 原文还原
}

# ============================================================
# 展开/折叠：搜索容器 三层压平；家具升级 多级压平
# ============================================================

def 展开搜索容器(地图类型列表):
    """地图类型[] -> 平铺 容器 行（带 地图类型/房间 前缀）"""
    rows = []
    for t in 地图类型列表:
        for r in t.get("房间", []) or []:
            for c in r.get("容器", []) or []:
                row = dict(c)
                row["地图类型"] = t.get("标识", "")
                row["房间"] = r.get("标识", "")
                rows.append(row)
    return rows

def 折叠搜索容器(rows):
    """平铺 容器 行 -> 地图类型[]（按 地图类型/房间 分组）"""
    from collections import OrderedDict
    地图 = OrderedDict()
    for row in rows:
        t = row.get("地图类型", ""); r = row.get("房间", "")
        c = {k: v for k, v in row.items() if k not in ("地图类型", "房间")}
        tmap = 地图.setdefault(t, {"标识": t, "名称": t, "房间": OrderedDict()})
        rmap = tmap["房间"].setdefault(r, {"标识": r, "名称": r, "容器": []})
        rmap["容器"].append(c)
    out = []
    for t in 地图.values():
        t["房间"] = list(t["房间"].values())
        out.append(t)
    return out

# ============================================================
# 读表数据（按 sheet 定义，从 JSON 收集行）
# ============================================================

def 收集表数据(表):
    sheet = 表["sheet"]
    # 物品类：从全部物品里按 类型 过滤
    if 表.get("类型过滤"):
        物品 = 加载物品()
        rows = [x for x in 物品 if x.get("类型") in 表["类型过滤"]]
        return rows
    # 搜索容器：三层压平
    if sheet == "搜索容器":
        地图类型 = _load_key(表["json"], 表["key"])
        return 展开搜索容器(地图类型)
    # 其他：单/多文件直接加载
    src = 表["json"]
    if isinstance(src, list):
        rows = []
        for p in src:
            rows.extend(_load_key(p, 表["key"]))
        return rows
    return _load_key(src, 表["key"])

# ============================================================
# to_xlsx：JSON -> xlsx
# ============================================================

def 填充行(ws, 表, rows):
    """写表头 + 数据行"""
    列 = 表["列"]
    # 表头
    ws.append([c[0] for c in 列])
    for row in rows:
        值 = []
        for (_excel, field, 解析器) in 列:
            v = row.get(field) if isinstance(row, dict) else None
            ser = _SER.get(解析器, _SER["str"])
            值.append(ser(v))
        ws.append(值)

def to_xlsx():
    物品 = 加载物品()
    wb = Workbook()
    wb.remove(wb.active)
    写说明页(wb)
    for 表 in TABLES:
        ws = wb.create_sheet(title=表["sheet"])
        rows = 收集表数据(表)
        填充行(ws, 表, rows)
        # 行宽（标识列放前面，给宽点）
        for i in range(1, len(表["列"]) + 1):
            ws.column_dimensions[chr(64 + i) if i <= 26 else 'A'].width = 14
    wb.save(XLSX_PATH)
    print("已生成：{}".format(XLSX_PATH))

def 写说明页(wb):
    ws = wb.create_sheet(title="使用说明")
    lines = [
        "《最后87天》游戏数据表使用说明",
        "",
        "1. 各 Sheet 与游戏 JSON 一一对应；改完保存后，在本目录执行：py 数据导出.py to_json",
        "2. 全角分号「；」用于分隔列表项，请勿在单元格内自行使用（否则会被当分隔符）。",
        "3. 嵌套字段约定：",
        "   - 材料：木板 x 5；布料 x 3",
        "   - 效果（天赋）：体质 +2；力量 -1",
        "   - 数值数组（家具效果/保质期倍率）：100；130；150",
        "   - 容器分区（净水器）：脏水 3x3 材料；过滤 3x2 材料；净水 3x3 材料",
        "   - 搜索表：木板 3:2~5；管道胶带 3:2~5  （物品 权重:数量最小~数量最大）",
        "   - 配方池（书籍）：制作_烤土豆；制作_煎蛋",
        "   - 职业属性分布：体质 5；力量 4",
        "",
        "4. bool 字段填「是/否」。",
        "5. 不要改「标识」列（标识 = 物品显示名 = 图标文件名，改了会断引用）。",
    ]
    for line in lines:
        ws.append([line])
    ws.column_dimensions['A'].width = 110

# ============================================================
# to_json：xlsx -> JSON
# ============================================================

def _cell(v):
    return "" if v is None else str(v).strip()

def 读取行(表, sheet_rows):
    """读 xlsx 数据行 -> dict 列表"""
    列 = 表["列"]
    headers = [_cell(r) for r in sheet_rows[0]]
    # 列名 -> 索引
    索引 = {}
    for i, h in enumerate(headers):
        索引[h] = i
    rows = []
    for 单元格行 in sheet_rows[1:]:
        if not 单元格行 or not any(_cell(c) != "" for c in 单元格行):
            continue
        row = {}
        present = False
        for (_excel, field, 解析器) in 列:
            idx = 索引.get(_excel, -1)
            if idx < 0:
                continue
            原始 = _cell(单元格行[idx]) if idx < len(单元格行) else ""
            if 原始 == "":   # 空单元格 → 不写（保持 JSON 干净：未填字段省略，等价于默认值）
                continue
            val = 反序列化(原始, 解析器)
            row[field] = val
            present = True
        # 值类型修正（int 字段给 int，避免导出带小数）
        for (_excel, field, 解析器) in 列:
            if field in row and 解析器 in ("int",):
                try:
                    row[field] = int(row[field])
                except (ValueError, TypeError):
                    pass
        if present:
            rows.append(row)
    return rows, 索引

def 反序列化(原始, 解析器):
    if 解析器 == "str":
        return 原始
    if 解析器 == "int":
        try:
            return int(float(原始)) if 原始 else 0
        except (ValueError, TypeError):
            return 0
    if 解析器 == "float":
        try:
            return float(原始) if 原始 else 0.0
        except (ValueError, TypeError):
            return 0.0
    if 解析器 == "bool":
        return _parse_bool(原始)
    func = _DESER_FOR_KEY.get(解析器)
    if func:
        return func(原始)
    return 原始

def to_json():
    wb = load_workbook(XLSX_PATH, data_only=True)
    # 物品：先收集 5 张物品表，合并成 物品 字典（按 类型 分文件写）
    物品行 = []
    # 武器/防具/材料 3 张表没有「类型」列（sheet 名已隐含类型）——回写时补上，避免丢类型
    固定类型 = {"物品·武器": "武器", "物品·防具": "防具", "物品·材料": "材料"}
    for 表 in TABLES:
        if 表["sheet"].startswith("物品·"):
            ws = wb[表["sheet"]]
            rows, _ = 读取行(表, list(ws.values))
            if 表["sheet"] in 固定类型:
                for row in rows:
                    row["类型"] = 固定类型[表["sheet"]]
            物品行.extend(rows)
    # 写物品（按 类型分文件）
    写物品到文件(物品行)

    # 其他表
    for 表 in TABLES:
        if 表["sheet"].startswith("物品·"):
            continue
        if 表["sheet"] not in wb.sheetnames:
            continue
        ws = wb[表["sheet"]]
        rows, _ = 读取行(表, list(ws.values))
        if 表["sheet"] == "搜索容器":
            地图类型 = 折叠搜索容器(rows)
            _save_json(os.path.normpath(表["json"]), {"地图类型": 地图类型})
            continue
        src = 表["json"]
        if isinstance(src, list):
            # 配方：合并写回 3 文件需按 类型 拆分
            if 表["sheet"] == "配方":
                写配方到文件(rows)
                continue
        _save_json(os.path.normpath(src), {表["key"]: rows})
    print("已导出 JSON 到：{}".format(DATA_DIR))

def 写物品到文件(物品行):
    # 按 类型 分文件（沿用现有文件结构）
    # 分组
    桶 = {}
    for row in 物品行:
        t = row.get("类型", "")
        if t == "防具":
            k = "防具_" + (row.get("槽位", "") or "其他")
            桶.setdefault(k, []).append(row)
        elif t == "材料":
            k = "材料_" + (row.get("种类", "") or "其他")
            桶.setdefault(k, []).append(row)
        else:
            桶.setdefault(t, []).append(row)
    # 写文件（记录目标路径，用后清理残留）
    目标路径 = set()
    for k, arr in 桶.items():
        path = 物品文件路径(k)
        _save_json(path, {"物品": arr})
        目标路径.add(os.path.normpath(path))
    # 清理残留旧物品文件（不在目标集合里的 json——如 类型缺失 兜底 的 items_其他.json）
    物品根 = os.path.join(DATA_DIR, "物品")
    for 根, _dirs, files in os.walk(物品根):
        for fn in files:
            if not fn.endswith(".json"):
                continue
            完整 = os.path.normpath(os.path.join(根, fn))
            if 完整 not in 目标路径:
                os.remove(完整)
                print("  清理残留：{}".format(os.path.relpath(完整, BASE_DIR)))

def 物品文件路径(键):
    if 键.startswith("防具_"):
        槽 = 键[len("防具_"):]
        return os.path.join(DATA_DIR, "物品", "防具", "items_防具_{}.json".format(槽))
    if 键.startswith("材料_"):
        种类 = 键[len("材料_"):]
        return os.path.join(DATA_DIR, "物品", "材料", "items_材料_{}.json".format(种类))
    文件映射 = {
        "武器": "items_武器.json", "饮食": "items_饮食.json", "医疗": "items_医疗.json",
        "容器": "items_容器.json", "弹药": "items_弹药.json", "任务品": "items_任务品.json",
        "技能书": "items_任务品.json", "书籍": "items_书籍.json",
    }
    return os.path.join(DATA_DIR, "物品", 文件映射.get(键, "items_其他.json"))

def 写配方到文件(rows):
    配方根 = os.path.join(DATA_DIR, "recipes_{}.json")
    桶 = {}
    for row in rows:
        t = row.get("类型", "工作台")
        桶.setdefault(t, []).append(row)
    for t, arr in 桶.items():
        _save_json(配方根.format(t), {"配方": arr})

# ============================================================
# 主入口
# ============================================================

if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "to_xlsx"
    if cmd in ("to_xlsx", "init"):
        to_xlsx()
    elif cmd == "to_json":
        to_json()
    else:
        print("用法：py 数据导出.py to_xlsx|to_json")