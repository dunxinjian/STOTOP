# -*- coding: utf-8 -*-
"""从最终映射生成 凭证规则3131 的 RulesBasedVoucherConfigV2 config + 校验。
处理:一码多名合并(同码归一组,科目异则组内ConditionField按费用名分流)、资金类编码还原(3.28→03.28)、借贷对手平衡。"""
import json, glob, os, zipfile, re
import xml.etree.ElementTree as ET
from collections import defaultdict, OrderedDict

NS = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
NEW_DIR = r"E:\STOTOP_Fable\Taicang\申通总部交易明细新-20260507开始"
FINAL = r"E:\STOTOP_Fable\Temp\_final_mapping.json"

# ---- 取真实编码集(权威,还原xlsx丢的前导0) ----
def col_to_idx(ref):
    m = re.match(r"([A-Z]+)\d+", ref); n=0
    for ch in m.group(1): n=n*26+(ord(ch)-64)
    return n-1
def read_sheet(path):
    z=zipfile.ZipFile(path); shared=[]
    if "xl/sharedStrings.xml" in z.namelist():
        root=ET.fromstring(z.read("xl/sharedStrings.xml"))
        for si in root.findall(f"{NS}si"): shared.append("".join(t.text or "" for t in si.iter(f"{NS}t")))
    root=ET.fromstring(z.read("xl/worksheets/sheet1.xml")); z.close()
    sd=root.find(f"{NS}sheetData"); rows=[]
    for row in sd.findall(f"{NS}row"):
        cells={}; mx=0
        for c in row.findall(f"{NS}c"):
            ci=col_to_idx(c.get("r")); t=c.get("t"); val=""
            if t=="s":
                v=c.find(f"{NS}v")
                if v is not None and v.text is not None: val=shared[int(v.text)]
            elif t=="inlineStr":
                ie=c.find(f"{NS}is")
                if ie is not None: val="".join(tt.text or "" for tt in ie.iter(f"{NS}t"))
            else:
                v=c.find(f"{NS}v"); val=v.text if (v is not None and v.text is not None) else ""
            cells[ci]=val; mx=max(mx,ci)
        rows.append([cells.get(i,"") for i in range(mx+1)])
    return rows
real_codes=set(); real_code_by_name={}
for p in sorted(glob.glob(os.path.join(NEW_DIR,"*"))):
    rows=read_sheet(p)
    if not rows: continue
    hdr=[str(x).strip() for x in rows[0]]; idx={h:i for i,h in enumerate(hdr)}
    ic,ina=idx.get("费用编码",-1),idx.get("费用名称",-1)
    for r in rows[1:]:
        code=str(r[ic]).strip() if 0<=ic<len(r) and r[ic] is not None else ""
        name=str(r[ina]).strip() if 0<=ina<len(r) and r[ina] is not None else ""
        if code: real_codes.add(code); real_code_by_name.setdefault(name, code)

def resolve_code(code, name):
    code=str(code).strip()
    if code in real_codes: return code
    if "0"+code in real_codes: return "0"+code               # 3.28 -> 03.28
    if name and name in real_code_by_name: return real_code_by_name[name]
    return code  # 兜底原样(后续校验会抓)

# ---- 载入最终映射 ----
fm=json.load(open(FINAL,encoding="utf-8"))["final"]

# ---- 按 exactCode 归组 ----
groups=OrderedDict()
for m in fm:
    rc=resolve_code(m["code"], m["name"])
    groups.setdefault(rc, []).append(m)

def aux_loss(bd):
    return [
        {"auxType":"outlet","sourceType":"dynamic","fixedItemId":None,"fixedItemCode":None,"fixedValue":None,"sourceField":"F网点编号","matchBy":"code"},
        {"auxType":"express_brand","sourceType":"fixed","fixedItemId":16,"fixedItemCode":"ST","fixedValue":None,"sourceField":None,"matchBy":"code"},
        {"auxType":"business_direction","sourceType":"fixed","fixedItemId":None,"fixedItemCode":None,"fixedValue":bd,"sourceField":None,"matchBy":"code"},
    ]
def aux_counterparty():
    return [{"auxType":"express_brand","sourceType":"fixed","fixedItemId":16,"fixedItemCode":"ST","fixedValue":None,"sourceField":None,"matchBy":"code"}]

ruleGroups=[]; order=0; multi=[]; warns=[]
for rc, items in groups.items():
    order+=1
    # 是否同码多科目/多方向 → 需组内ConditionField分流
    sigs=set((it["accountCode"],it["direction"],it["amountField"],it["businessDirection"]) for it in items)
    name=f"{rc} {items[0]['name']}" + ("" if len(items)==1 else f"(+{len(items)-1})")
    lines=[]; lineno=0
    if len(sigs)==1:
        it=items[0]
        cp_dir = "贷" if it["direction"]=="借" else "借"
        lineno+=1
        lines.append({"lineNo":lineno,"direction":it["direction"],"accountId":int(it["accountFid"]),"accountCode":it["accountCode"],
                      "amountField":it["amountField"],"summaryTemplate":"{F业务摘要}","status":1,"displayOrder":lineno,
                      "auxiliaryConfigs":aux_loss(it["businessDirection"])})
        lineno+=1
        lines.append({"lineNo":lineno,"direction":cp_dir,"accountId":700125,"accountCode":"220201",
                      "amountField":it["amountField"],"summaryTemplate":"{F业务摘要}","status":1,"displayOrder":lineno,
                      "auxiliaryConfigs":aux_counterparty()})
    else:
        # 组内按费用名称分流(ConditionField)
        multi.append((rc,[(it["name"],it["accountCode"],it["direction"]) for it in items]))
        for it in items:
            cp_dir = "贷" if it["direction"]=="借" else "借"
            lineno+=1
            lines.append({"lineNo":lineno,"direction":it["direction"],"accountId":int(it["accountFid"]),"accountCode":it["accountCode"],
                          "amountField":it["amountField"],"summaryTemplate":"{F业务摘要}","status":1,"displayOrder":lineno,
                          "conditionField":"F费用名称","conditionValues":[it["name"]],
                          "auxiliaryConfigs":aux_loss(it["businessDirection"])})
            lineno+=1
            lines.append({"lineNo":lineno,"direction":cp_dir,"accountId":700125,"accountCode":"220201",
                          "amountField":it["amountField"],"summaryTemplate":"{F业务摘要}","status":1,"displayOrder":lineno,
                          "conditionField":"F费用名称","conditionValues":[it["name"]],
                          "auxiliaryConfigs":aux_counterparty()})
    ruleGroups.append({"id":f"g{order:03d}","name":name,"order":order,"exactCodes":[rc],
                       "exactCategories":None,"categoryKeywords":None,"summaryKeywords":None,
                       "fallthrough":False,"amountAggregation":"ROW","lines":lines})
    if rc not in real_codes:
        warns.append(f"exactCode {rc} 不在真实数据编码集!")

config={"mode":"rulesBased","version":2,"voucherWord":"记","dateField":"F业务日期",
        "stagingTable":"STG申通总部交易明细","accountSetId":2,
        "keyFields":["F科目编码","F费用名称","F业务摘要"],
        "unmatchedAction":"createDraft","draftPlaceholderAccountId":700044,
        "filterConditions":[],
        "matchingLayers":{"exactMatchField":"F科目编码","categoryField":"F费用名称","summaryField":"F业务摘要"},
        "ruleGroups":ruleGroups}

json.dump(config, open(r"E:\STOTOP_Fable\Temp\_rule3131_config.json","w",encoding="utf-8"), ensure_ascii=False)
cfg_min=json.dumps(config, ensure_ascii=False, separators=(",",":"))
open(r"E:\STOTOP_Fable\Temp\_rule3131_config.min.json","w",encoding="utf-8").write(cfg_min)

print(f"ruleGroups={len(ruleGroups)} (源映射{len(fm)}条 → 归并{len(groups)}组)")
print(f"同码多科目需分流: {len(multi)}")
for rc,its in multi: print(f"  {rc}: {its}")
print(f"\n校验警告: {len(warns)}")
for w in warns: print("  ", w)
# 借贷自检:每组line1+line2方向相反、金额列一致
bad=0
for g in ruleGroups:
    ls=g["lines"]
    for i in range(0,len(ls),2):
        a,b=ls[i],ls[i+1]
        if a["direction"]==b["direction"] or a["amountField"]!=b["amountField"]: bad+=1
print(f"借贷配对自检异常: {bad}")
print(f"config字符数: {len(cfg_min)}")
# 用到的accountId集
print(f"用到accountId数: {len(set(l['accountId'] for g in ruleGroups for l in g['lines']))}")
