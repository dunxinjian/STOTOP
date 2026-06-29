# -*- coding: utf-8 -*-
"""
阿米巴批次6 —— 72 项损益项种子生成器
读  : 2026-06-15-阿米巴损益项映射.csv (72 行) + 2026-06-15-新科目表.csv (科目表)
产出: 2026-06-17-阿米巴损益项种子.sql  (嵌入 FinanceSeeder.cs 的种子块)
规则: 终态设计 spec §7.1~§7.8 + 用户决策(操作工资按 business_direction 分出/进港)

口径要点:
  - FID 600 起按 CSV 前序递增; F父ID 按层级栈; F排序 同父步长 10
  - group/小计 不配科目(SUM_CHILDREN); 仅 data 叶配 V2 F关联科目JSON
  - 出港 data 叶 filter=[business_direction OUT]; 进港=[IN]; 公共(综合)叶无 business_direction filter
  - 工资按 §7.3 末级码+部门; 操作工资(5601010103)改用 business_direction 分向(用户决策)
  - 科目缩写展开/补集靠科目表; 件量分摊→volume(基数 send/deliver/total), 其余→直接归段(null)
"""
import csv, io, json, os, sys
try: sys.stdout.reconfigure(encoding="utf-8")
except Exception: pass

BASE = os.path.dirname(os.path.abspath(__file__))
MAP_CSV   = os.path.join(BASE, "2026-06-15-阿米巴损益项映射.csv")
CHART_CSV = os.path.join(BASE, "2026-06-15-新科目表.csv")
OUT_SQL   = os.path.join(BASE, "2026-06-17-阿米巴损益项种子.sql")
TEMPLATE_ID = 3
FID0 = 600
TS = "2026-06-17 00:00:00.000"

# ── 科目表: 读末级码集 + 父→子 关系 ──────────────────────────────
chart = {}        # code -> dict(name, leaf, parent, aux)
children = {}     # parent_code -> [child_code...]
with open(CHART_CSV, encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        c = r["F编码"].strip()
        chart[c] = dict(name=r["F名称"], leaf=int(r["F是否末级"]), parent=r["F父编码"].strip(), aux=r["F辅助核算"] or "")
        p = r["F父编码"].strip()
        children.setdefault(p, []).append(c)

def leaves_under(prefix):
    """返回 prefix 前缀下所有末级码(含 prefix 自身若为末级)。"""
    out = []
    for c, info in chart.items():
        if c.startswith(prefix) and info["leaf"] == 1:
            out.append(c)
    return sorted(out)

def assert_code(c):
    if c not in chart:
        WARN.append(f"科目码不在科目表: {c}")
    return c

WARN = []

# ── 业务方向 / 部门 占位码(§7.3, 真实码以辅助核算项为准) ────────────
DEP = dict(揽件员="YWY_OUT", 派件员="YWY_IN", 客服="KF", 操作="CZ", 业务管理="YWGL")

# ── 工资/特殊项 显式映射: (段,损益项) -> [(code,[(auxType,[codes])...])...] ──
#   工资 CSV 写父码 56010101, 实际按角色落具体末级码 + 部门(§7.3)
#   操作工资 用户决策: 按 business_direction 分向(不用部门 CZ, 5601010103 本身即"操作")
def specials():
    return {
        ("出港","揽件员工资"): [("5601010101", [("department",["YWY_OUT"])])],
        ("进港","派件员工资"): [("5601010101", [("department",["YWY_IN"])])],
        ("进港","客服成本"):   [("5601010102", [("department",["KF"])]),
                                ("560121",     [("department",["KF"])])],
        ("出港","操作工资"):   [("5601010103", [("business_direction",["OUT"])])],
        ("进港","操作工资"):   [("5601010103", [("business_direction",["IN"])])],
        # 管理费用 = 5602补集(排除工资/水电折旧摊销租金) + 业务管理(5601010105/YWGL)
        ("公共","管理费用"):   None,   # 见 build_admin_codes()
    }

def build_admin_codes():
    """管理费用 = 5602 末级 排除 {薪酬56020101-subtree, 折旧560202, 摊销560203, 租金560204, 水电560205}
       + 业务管理 5601010105(部门 YWGL)。"""
    excluded_prefix = ("560201",)            # 5602 薪酬子树(工资/社保福利)
    excluded_exact  = {"560202","560203","560204","560205"}  # 已入公共分摊小计
    codes = []
    for c in leaves_under("5602"):
        if any(c.startswith(p) for p in excluded_prefix): continue
        if c in excluded_exact: continue
        codes.append((c, []))                # 综合管理费用叶, 无 business_direction filter
    codes.append(("5601010105", [("department",["YWGL"])]))
    return codes

# ── 科目集合 缩写展开(§7.7) ─────────────────────────────────────
def expand_codes(raw, seg):
    """把 CSV 科目集合 展开成 [code...]; 处理 + / 与 '其余末级' '补集'。"""
    raw = raw.strip()
    if not raw: return []
    # 其余末级: 'NNNNNN其余末级' = 该前缀下所有末级 减 已被同级兄弟占用的
    if raw.endswith("其余末级"):
        prefix = raw.replace("其余末级","")
        used = SIBLING_USED.get((seg, prefix), set())
        return [c for c in leaves_under(prefix) if c not in used]
    parts = []
    # 先按 '+' 再按 '/'; 短段继承前一码同位前缀
    tokens = []
    for plus in raw.split("+"):
        for slash in plus.split("/"):
            tokens.append(slash.strip())
    prev = None
    for t in tokens:
        if prev and len(t) < len(prev):
            t = prev[:len(prev)-len(t)] + t   # 继承前缀, 如 54010403 后的 '04' -> 54010404
        parts.append(t); prev = t
    return parts

# ── 读映射 CSV ─────────────────────────────────────────────────
rows = []
with open(MAP_CSV, encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        rows.append(r)

# 先收集所有"其余末级"前缀(seg, prefix), 不写死位数
REMAINDER_PREFIXES = {(r["段"], r["科目集合"].strip().replace("其余末级",""))
                      for r in rows if r["科目集合"].strip().endswith("其余末级")}
# 预扫描: 兄弟末级按匹配的"其余末级"前缀归桶(供补集; 与补集查键 (seg,prefix) 同源, 不假设长度)
SIBLING_USED = {}
for r in rows:
    seg, acct = r["段"], r["科目集合"].strip()
    if not acct or acct.endswith("其余末级"): continue
    if r["类型"] in ("细分","收入","成本","成本②","成本②+外包","分摊"):
        for c in expand_codes(acct, seg):
            for (s, p) in REMAINDER_PREFIXES:
                if s == seg and c.startswith(p):
                    SIBLING_USED.setdefault((s, p), set()).add(c)

SPECIAL = specials()
SPECIAL[("公共","管理费用")] = build_admin_codes()

# ── 类型 -> 角色/正交列 ─────────────────────────────────────────
def role_of(typ):
    if typ == "段" or typ.endswith("组") or typ.endswith("·小计"):
        return ("group","section",None,None,0)
    if typ == "指标":
        return ("indicator","indicator","manual",None,1)
    if typ in ("边际","净利"):
        return ("formula","profit","formula",None,0)
    # data 叶
    cat = "revenue" if typ in ("收入",) else "cost"
    if typ == "细分": cat = None   # 由父定
    return ("data",cat,"system","voucher",0)

UNIT = {"发件票量":"票","发件重量":"kg","均重":"kg/票","计价件量":"票","揽件员人数":"人","揽件效能":"件/人/日",
        "派件票量":"票","派件重量":"kg","入库率":"%","派件员人数":"人","派件效能":"件/人/日"}

FORMULA = {"出港边际利润":"${出港收入} - ${出港成本}",
           "进港边际利润":"${进港收入} - ${进港成本}",
           "经营净利润":"${出港边际利润} + ${进港边际利润} - ${公共分摊}"}

def alloc_of(r, seg):
    """分摊: 件量分摊->volume(基数 send/deliver/total); 其余->None(直接归段)。"""
    a = r["分摊"].strip()
    if a.startswith("件量分摊"):
        if "综合" in r["业务方向"] or seg == "公共": basis = "total"
        elif seg == "进港": basis = "deliver"
        else: basis = "send"
        return ("volume", basis)
    return (None, None)

# ── 建项 ───────────────────────────────────────────────────────
items = []           # dict per row
stack = []           # [(level, fid)...] 最近祖先栈(容忍层级跳跃, 如公共 L1->L3)
sort_ctr = {}        # parentFID -> running sort
name_seen = {}

for idx, r in enumerate(rows):
    fid = FID0 + idx
    lvl = int(r["层级"])
    while stack and stack[-1][0] >= lvl:
        stack.pop()
    parent = stack[-1][1] if stack else 0
    stack.append((lvl, fid))
    sort_ctr[parent] = sort_ctr.get(parent, 0) + 10
    name = r["损益项"]
    seg = r["段"]
    role, cat, valsrc, sysrc, manual = role_of(r["类型"])
    if cat is None:   # 细分: 由父项(小计)的 CSV 类型判 revenue/cost(group cat 恒为 section, 不能继承)
        ptyp = next((it["typ"] for it in items if it["fid"] == parent), "")
        cat = "revenue" if ptyp.startswith("收入") else "cost"

    # 跨段重名 data 叶加段前缀(§7.5③): 操作工资
    disp = name
    if role == "data" and name == "操作工资":
        disp = seg + "操作工资"

    # V2 关联科目
    acc_json = None
    if role == "data":
        sp = SPECIAL.get((seg, name))
        if sp is not None:
            specs = [{"code":assert_code(c), **({"filters":[{"auxType":at,"codes":cs} for at,cs in fl]} if fl else {})} for c,fl in sp]
        else:
            codes = expand_codes(r["科目集合"], seg)
            for c in codes: assert_code(c)
            filt = []
            bd = {"出港":"OUT","进港":"IN"}.get(seg)
            if bd: filt = [{"auxType":"business_direction","codes":[bd]}]
            specs = [{"code":c, **({"filters":filt} if filt else {})} for c in codes]
        acc_json = json.dumps(specs, ensure_ascii=False)

    alloc_m, alloc_b = (None,None)
    if role == "data": alloc_m, alloc_b = alloc_of(r, seg)

    unit = None; perunit = None
    if role == "data": unit, perunit = "元","auto"
    elif role == "indicator": unit, perunit = UNIT.get(name,"票"),"none"
    elif role == "formula": unit, perunit = "元","none"

    formula = FORMULA.get(name) if role == "formula" else None
    src_remark = r["取数源_月"].strip() or None if role in ("data",) else None
    calc = None
    if role == "data":
        calc = (f"件量分摊({alloc_b});{r['单票基数']}为单票基数" if alloc_m=="volume"
                else "直接归段")
    elif role == "formula":
        calc = formula

    items.append(dict(fid=fid, parent=parent, sort=sort_ctr[parent], name=disp, raw_name=name,
                      seg=seg, role=role, cat=cat, valsrc=valsrc, sysrc=sysrc, manual=manual,
                      acc=acc_json, alloc_m=alloc_m, alloc_b=alloc_b, unit=unit, perunit=perunit,
                      formula=formula, src_remark=src_remark, calc=calc, typ=r["类型"]))
    name_seen.setdefault(disp, []).append(fid)

# ── 自检(§7.8) ─────────────────────────────────────────────────
def selfcheck():
    # P0-1: voucher 收入/成本 data 叶必有非空 F关联科目JSON
    for it in items:
        if it["role"]=="data" and it["valsrc"]=="system" and it["sysrc"]=="voucher":
            if not it["acc"] or it["acc"]=="[]":
                WARN.append(f"[P0-1] data 叶无关联科目: {it['name']}({it['fid']})")
    # 同码方向重叠(部门感知): 同为 voucher data 叶, code 前缀相关 且 无任一维度区分
    vd = [it for it in items if it["role"]=="data" and it["sysrc"]=="voucher"]
    def dims(it):
        specs = json.loads(it["acc"]) if it["acc"] else []
        out=[]
        for s in specs:
            bd=set(); dep=set()
            for fil in s.get("filters",[]):
                if fil["auxType"]=="business_direction": bd|=set(fil["codes"])
                if fil["auxType"]=="department": dep|=set(fil["codes"])
            out.append((s["code"], bd, dep))
        return out
    for i in range(len(vd)):
        for j in range(i+1,len(vd)):
            for c1,bd1,dep1 in dims(vd[i]):
                for c2,bd2,dep2 in dims(vd[j]):
                    if c1.startswith(c2) or c2.startswith(c1):
                        bd_sep  = bd1 and bd2 and not (bd1 & bd2)
                        dep_sep = dep1 and dep2 and not (dep1 & dep2)
                        if not bd_sep and not dep_sep:
                            WARN.append(f"[重叠] {vd[i]['name']}({c1}) ↔ {vd[j]['name']}({c2}) 无维度区分")
    # 跨段重名 data 叶 filters 必含 business_direction 或 department
    byname={}
    for it in items:
        if it["role"]=="data": byname.setdefault(it["raw_name"],[]).append(it)
    for nm,its in byname.items():
        if len(its)>1 and len({it["seg"] for it in its})>1:
            for it in its:
                specs=json.loads(it["acc"]) if it["acc"] else []
                ok=all(any(fil["auxType"] in ("business_direction","department") for fil in s.get("filters",[])) for s in specs)
                if not ok: WARN.append(f"[跨段重名] {it['name']} filters 缺方向/部门维度")
    # 公式引用名存在
    names={it["name"] for it in items}
    import re
    for it in items:
        if it["formula"]:
            for ref in re.findall(r"\$\{([^}]+)\}", it["formula"]):
                if ref not in names: WARN.append(f"[公式] {it['name']} 引用不存在项 ${{{ref}}}")

selfcheck()

# ── 生成 SQL ───────────────────────────────────────────────────
COLS = ["FID","F模板ID","F项目名称","F节点角色","F计算公式","F排序","F父ID","F关联科目JSON","F计费过滤Json",
        "F值来源","F系统数据源","F是否手工填报","F项目类别","F是否指标分区","F单位","F单票均模式",
        "F小数位数","F分摊方式","F分摊基数","F数据来源说明","F计算逻辑","F创建时间","F更新时间"]
assert "F节点角色" in COLS, "建表 NOT NULL 列 F节点角色 必须显式落库"

def sv(v):
    if v is None: return "NULL"
    if isinstance(v, bool): return "1" if v else "0"
    if isinstance(v, int): return str(v)
    return "N'" + str(v).replace("'","''") + "'"

def row_sql(it):
    vals = [it["fid"], TEMPLATE_ID, it["name"], it["role"], it["formula"], it["sort"], it["parent"],
            it["acc"], None, it["valsrc"], it["sysrc"], it["manual"], it["cat"], 0,
            it["unit"], it["perunit"], None, it["alloc_m"], it["alloc_b"],
            it["src_remark"], it["calc"], TS, TS]
    assert len(vals) == len(COLS), f"列数({len(COLS)})与值数({len(vals)})不匹配: {it['fid']}"
    return "INSERT INTO [FIN阿米巴损益项] (" + ", ".join(f"[{c}]" for c in COLS) + ") VALUES (" + ", ".join(sv(v) for v in vals) + ");"

def render(indent, double_quotes):
    body = "\n".join(indent + "    " + row_sql(it) for it in items)
    if double_quotes:
        body = body.replace('"', '""')
    return (
        f"{indent}SET IDENTITY_INSERT [FIN阿米巴损益项] ON;\n\n"
        f"{indent}-- 批次6: 一次性守卫重灌(§7.7) —— FID {FID0} 缺失即清模板{TEMPLATE_ID}并灌新 {len(items)} 项\n"
        f"{indent}IF NOT EXISTS (SELECT 1 FROM [FIN阿米巴损益项] WHERE [FID] = {FID0})\n"
        f"{indent}BEGIN\n"
        f"{indent}    -- 仅清种子两段(旧414-495 + 新{items[0]['fid']}-{items[-1]['fid']}), 不动用户运行期自定义项(其他FID)\n"
        f"{indent}    DELETE FROM [FIN阿米巴损益项] WHERE [F模板ID] = {TEMPLATE_ID} AND ([FID] BETWEEN 414 AND 495 OR [FID] BETWEEN {items[0]['fid']} AND {items[-1]['fid']});\n\n"
        f"{body}\n"
        f"{indent}END\n\n"
        f"{indent}SET IDENTITY_INSERT [FIN阿米巴损益项] OFF;\n"
    )

# 标准 SQL(单引号, 可独立审阅/运行)
with open(OUT_SQL, "w", encoding="utf-8") as f:
    f.write(render("        ", double_quotes=False))
# C# 逐字串就绪版(引号翻倍, 8 空格缩进, 直接粘进 ExecSql(ctx, @"..."))
OUT_CS = os.path.join(BASE, "2026-06-17-阿米巴损益项种子.cs.txt")
with open(OUT_CS, "w", encoding="utf-8") as f:
    f.write(render("        ", double_quotes=True))

# ── 报告 ───────────────────────────────────────────────────────
print(f"项数: {len(items)}  (group={sum(1 for i in items if i['role']=='group')}, "
      f"data={sum(1 for i in items if i['role']=='data')}, "
      f"indicator={sum(1 for i in items if i['role']=='indicator')}, "
      f"formula={sum(1 for i in items if i['role']=='formula')})")
print(f"FID: {items[0]['fid']}..{items[-1]['fid']}")
print(f"SQL -> {OUT_SQL}")
print("\n各 data 叶关联科目:")
for it in items:
    if it["role"]=="data":
        print(f"  {it['fid']:>3} [{it['seg']}] {it['name']:<14} {it['alloc_m'] or '直接':<6} {it['acc']}")
print("\n自检告警:" if WARN else "\n自检: 全部通过 ✓")
for w in WARN: print("  ! " + w)
