# -*- coding: utf-8 -*-
import re, io, csv
txt = open("src/STOTOP.WebAPI/Data/Seeders/FinanceSeeder.cs", encoding="utf-8", errors="ignore").read()

# ---------- 1. 解析现有 FIN科目 ----------
def parse_fin():
    rows={}
    pat=re.compile(r"INSERT INTO \[FIN科目\] \(([^)]*)\) VALUES \((.*?)\);", re.S)
    for m in pat.finditer(txt):
        cols=[c.strip().strip("[]") for c in m.group(1).split(",")]
        raw=m.group(2); vals=[]; cur=""; inq=False
        for ch in raw:
            if ch=="'": inq=not inq; cur+=ch
            elif ch=="," and not inq: vals.append(cur.strip()); cur=""
            else: cur+=ch
        vals.append(cur.strip())
        def cl(v):
            v=v.strip()
            if v.startswith("N'") and v.endswith("'"): return v[2:-1]
            if v=="NULL": return ""
            return v
        d={c:cl(v) for c,v in zip(cols,vals)}
        code=d.get("F编码","")
        if code and code not in rows:   # 去重，保首条
            rows[code]=dict(name=d.get("F名称",""), cat=d.get("F类别",""),
                            dir=d.get("F余额方向",""), aux=d.get("F辅助核算",""))
    return rows
EX = parse_fin()

# ---------- 2. 排除将重建的段 ----------
def excluded(code):
    if code.startswith("5001") or code.startswith("5401"): return True
    if code.startswith("1012"): return True
    if code.startswith("2202"): return True
    if code=="560125": return True   # 驿站费用 移入主营
    if code in ("505102","540203"): return True   # 快件理赔受款/赔款 → 并入主营客服理赔
    if code.startswith("1002") and len(code)>4: return True  # 银行子科目→改用太仓美申账套2
    return False
chart={c:v for c,v in EX.items() if not excluded(c)}

# 个别既有科目的辅助核算/名称订正
OVR={
 "2203":dict(aux="客户"), "1123":dict(aux="供应商"),
 "224102":dict(aux="供应商"), "221103":dict(name="职工保险"),
}
for c,o in OVR.items():
    if c in chart: chart[c].update(o)

# ---------- 3. 新增/重建段 ----------
MI="网点,业务方向,快递品牌,经营单元"           # 主营默认辅助
NEW=[]
def add(code,name,aux="",cat=None,dr=None):
    NEW.append((code,name,aux,cat,dr))

# 5001 主营业务收入
add("5001","主营业务收入","",cat="营业收入",dr="贷")
inc=[("500101","出港收入",MI+",客户,项目",
        ["现付收入","月结收入","到付收入","总部平台单"]),
     ("500102","派费",MI,
        ["基础派费","补贴派费","大货派费","周期派费","调整派费","按需派费","扶持派费","小件员权益","考核派费","VIP专享派费","政策互惠"]),
     ("500103","操作",MI,["退件操作费","其他操作费"]),
     ("500104","增值服务",MI,["网点线路跑车"]),
     ("500105","政策",MI,["政策返利","政策考核"]),
     ("500106","考核激励",MI,["质量考核","时效考核","操作规范考核","客服类考核","车辆考核","网管类考核","工单考核","签收率考核","时效件投诉考核","虚假问题件考核","末端类考核","省区综合考核","管控类","KPI激励","扶持基金"]),
     ("500107","客服",MI,["客服受款","三件私了"])]
for code,name,aux,subs in inc:
    add(code,name,aux,cat="营业收入",dr="贷")
    for i,s in enumerate(subs,1):
        sc=("99" if s=="杂项" else f"{i:02d}")
        add(code+sc, s, aux, cat="营业收入", dr="贷")

# 5401 主营业务成本
add("5401","主营业务成本","",cat="营业成本",dr="借")
cost=[("540101","面单",MI,["面单费"]),
      ("540102","派费",MI+",供应商,员工",
         ["基础派费","补贴派费","大货派费","周期派费","调整派费","按需派费","扶持派费","小件员权益","考核派费","代派费","承包区派费","驿站费用"]),
      ("540103","中转",MI,["中转费","中转加收","中转考核","全网出港费","其他中转费"]),
      ("540104","操作",MI,["中心集包费","三方集包费","退件操作费","其他操作费","装卸费"]),
      ("540105","增值服务",MI,["经营支持服务费","网点赋能","网格仓服务费","智橙网服务费","信息技术服务费"]),
      ("540106","考核罚款",MI,["质量考核","时效考核","操作规范考核","客服类考核","车辆考核","网管类考核","工单考核","签收率考核","时效件投诉考核","虚假问题件考核","末端类考核","省区综合考核","管控类罚款"]),
      ("540107","客服",MI,["客服赔款","三件私了"]),
      ("540108","物料",MI,["辅料","环保袋"]),
      ("540109","运输",MI,["燃油费","过路费","维修费","违章罚款","保险费","检车费","进门/停车费","租车费用","新能源车电费","购置税","汽运线路费","车辆折旧","其他费用"])]
for code,name,aux,subs in cost:
    add(code,name,aux,cat="营业成本",dr="借")
    for i,s in enumerate(subs,1):
        sc=("99" if s=="杂项" else f"{i:02d}")
        add(code+sc, s, aux, cat="营业成本", dr="借")

# 1012 其他货币资金（总部预付款，按品牌）
add("1012","其他货币资金","",cat="流动资产",dr="借")
for i,b in enumerate(["申通预付款","韵达预付款","极兔预付款","中通预付款","圆通预付款（预留）"],1):
    add(f"1012{i:02d}", b, "网点", cat="流动资产", dr="借")

# 1002 银行存款（太仓美申 账套2 真实子科目）
for code,name in [("100201","中国银行太仓支行"),("100203","工行-敦新建"),("100204","浏河建鑫"),
                  ("100205","沙溪美鑫"),("100206","韵科-中行"),("100207","韵科-建行"),
                  ("100208","韵达-瑞予宏"),("100210","极兔傲速")]:
    add(code,name,"",cat="流动资产",dr="借")
add("100209","支付宝-对公","",cat="流动资产",dr="借")
for sc,nm in [("01","支付宝@163"),("02","支付宝8960"),("03","支付宝-建鑫"),("04","支付宝-美鑫")]:
    add("100209"+sc, nm, "", cat="流动资产", dr="借")

# 2202 应付账款
add("2202","应付账款","",cat="流动负债",dr="贷")
for code,name,aux in [("220201","总部应付","快递品牌"),("220202","供应商应付","供应商"),("220299","其他应付","")]:
    add(code,name,aux,cat="流动负债",dr="贷")

# 1123 预付账款 子目
add("112301","供应商预付","供应商",cat="流动资产",dr="借")
add("112302","预付驿站款","供应商",cat="流动资产",dr="借")
# 2203 预收账款 子目
add("220301","预购面单预收","客户",cat="流动负债",dr="贷")
add("220399","其他预收","客户",cat="流动负债",dr="贷")
# 2241 其他应付款 补子目
add("224101","代收货款","客户",cat="流动负债",dr="贷")
add("224110","客户待退款","客户",cat="流动负债",dr="贷")
# 5402 其他业务成本 补"其他成本"子目
add("540299","其他成本","部门",cat="营业成本",dr="借")
# 56010101 工资【正式工】 按岗位拆
for sc,nm in [("01","业务员"),("02","客服"),("03","操作"),("04","司机"),("05","业务管理"),("06","其他")]:
    add("56010101"+sc, nm, "部门", cat="期间费用", dr="借")

# ---------- 4. 合并 ----------
final={}
for c,v in chart.items():
    final[c]=dict(name=v["name"],cat=v["cat"],dir=v["dir"],aux=v.get("aux",""))
for code,name,aux,cat,dr in NEW:
    final[code]=dict(name=name,cat=cat or final.get(code,{}).get("cat",""),
                     dir=dr or final.get(code,{}).get("dir",""),aux=aux)

# 计算 级次/父/末级
codes=sorted(final.keys())
childset=set()
for c in codes:
    if len(c)>4 and c[:-2] in final: childset.add(c[:-2])
rows=[]
for c in codes:
    lvl=1 if len(c)<=4 else len(c)//2  # 4位=1, 6=3?  -> 用 (len-2)//2
    lvl=max(1,(len(c)-2)//2) if len(c)>4 else 1
    parent=c[:-2] if (len(c)>4 and c[:-2] in final) else ""
    leaf=0 if c in childset else 1
    v=final[c]
    rows.append([c,v["name"],v["cat"],v["dir"],lvl,parent,leaf,v["aux"]])

# ---------- 5. 输出 CSV ----------
with open("docs/superpowers/specs/2026-06-15-新科目表.csv","w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f); w.writerow(["F编码","F名称","F类别","F余额方向","F级次","F父编码","F是否末级","F辅助核算"])
    w.writerows(rows)

# ---------- 6. 输出 树 ----------
out=io.StringIO()
bycat={}
for r in rows: bycat.setdefault(r[0][:1],[]).append(r)
def tree_for(prefixes,title):
    out.write(f"\n{'='*70}\n{title}\n{'='*70}\n")
    sel=[r for r in rows if any(r[0].startswith(p) for p in prefixes)]
    for r in sel:
        ind="  "*(r[4]-1)
        leaf="" if r[6]==0 else "·末"
        aux=f"  [辅:{r[7]}]" if r[7] else ""
        out.write(f"{ind}{r[0]} {r[1]}{leaf}{aux}\n")
tree_for(["1"],"一、资产")
tree_for(["2"],"二、负债")
tree_for(["3"],"三、所有者权益")
tree_for(["4"],"四、成本（制造业，保留）")
tree_for(["5001","5051"],"五、营业收入")
tree_for(["5101","5111","5121","5151","5301"],"六、其他收益/营业外收入")
tree_for(["5401","5402","5403"],"七、营业成本/税金及附加")
tree_for(["5601","5602","5603"],"八、期间费用")
tree_for(["5701","5711","5801","5901"],"九、其他损失/所得税/调整")
open("docs/superpowers/specs/2026-06-15-新科目表_树.txt","w",encoding="utf-8").write(out.getvalue())

# 统计
from collections import Counter
catc=Counter(r[2] for r in rows)
leafc=sum(1 for r in rows if r[6]==1)
print("TOTAL",len(rows),"LEAF",leafc)
print("CATS",dict(catc))
