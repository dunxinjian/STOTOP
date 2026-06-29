# -*- coding: utf-8 -*-
import csv, re, io
SEEDER="src/STOTOP.WebAPI/Data/Seeders/FinanceSeeder.cs"
CHART="docs/superpowers/specs/2026-06-15-新科目表.csv"

# ── 辅助核算 中文→英文 code ──
AUX={"网点":"outlet","业务方向":"business_direction","快递品牌":"express_brand","经营单元":"business_unit",
     "客户":"customer","供应商":"supplier","部门":"department","员工":"employee","项目":"project","outlet":"outlet"}
def tr_aux(s):
    if not s or s.strip() in("","_"): return None
    parts=[AUX.get(p.strip(),p.strip()) for p in s.split(",") if p.strip()]
    return ",".join(dict.fromkeys(parts)) if parts else None

# ── 1. 新科目表(排除 1002*/1012*) ──
COMMON=[]
with open(CHART,encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        c=r["F编码"]
        if c.startswith("1002") or c.startswith("1012"): continue
        COMMON.append(dict(code=c,name=r["F名称"],cat=r["F类别"],dir=r["F余额方向"],
                           lvl=int(r["F级次"]),parent=r["F父编码"],leaf=int(r["F是否末级"]),aux=tr_aux(r["F辅助核算"])))

# ── 2. 抽取两账套现有 1002/1012 + 各账套组织ID ──
txt=open(SEEDER,encoding="utf-8",errors="ignore").read()
pat=re.compile(r"INSERT INTO \[FIN科目\] \([^)]*\) VALUES \((.*?)\);",re.S)
def splitvals(raw):
    vals=[];cur="";inq=False
    for ch in raw:
        if ch=="'":inq=not inq;cur+=ch
        elif ch==","and not inq:vals.append(cur.strip());cur=""
        else:cur+=ch
    vals.append(cur.strip());return vals
def clean(v):
    v=v.strip()
    if v.startswith("N'")and v.endswith("'"):return v[2:-1]
    if v=="NULL":return None
    return v
keep={"1":[], "2":[]}   # 账套ID -> 1002/1012 行
orgof={}
for m in pat.finditer(txt):
    v=[clean(x) for x in splitvals(m.group(1))]
    if len(v)<14: continue
    code,name,cat,dr,lvl,_pid,leaf,aux=v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8]
    setid,org=v[12],v[13]
    if setid in keep and (str(code).startswith("1002") or str(code).startswith("1012")):
        keep[setid].append(dict(code=code,name=name,cat=cat,dir=dr,lvl=int(lvl),
                                parent=(code[:-2] if len(code)>4 else ""),leaf=int(leaf),aux=aux))
    if setid in("1","2") and setid not in orgof: orgof[setid]=org

# ── 3. 组装每账套全量 + 分配新FID + 解析父ID ──
FID0={"1":600000,"2":700000}
def assemble(setid):
    rows=keep[setid]+COMMON
    # 排序：父在子前(按编码长度+编码)
    rows=sorted(rows,key=lambda r:(len(r["code"]),r["code"]))
    code2fid={};out=[];fid=FID0[setid]
    for r in rows:
        fid+=1;code2fid[r["code"]]=fid;r["_fid"]=fid
    for r in rows:
        r["_pid"]=code2fid.get(r["parent"],0) if r["parent"] else 0
        out.append(r)
    return out,orgof.get(setid,"0")

def sqlval(x):
    if x is None:return "NULL"
    if isinstance(x,int):return str(x)
    return "N'"+str(x).replace("'","''")+"'"

buf=io.StringIO()
buf.write("-- 科目覆盖迁移：石家庄申通(账套1)+太仓美申(账套2)，保留各账套 1002/1012，其余用新设计科目覆盖\n")
buf.write("-- 开发期无凭证，安全删建；全新 FID。生成自 docs/.../2026-06-15-新科目表.csv\n")
buf.write("SET NOCOUNT ON;\nBEGIN TRAN;\n\n")
buf.write("DELETE FROM [FIN科目] WHERE [F账套ID] IN (1,2);\n\n")
cols="[FID],[F编码],[F名称],[F类别],[F余额方向],[F级次],[F父ID],[F是否末级],[F辅助核算],[F外币],[F计算单位],[F启用状态],[F账套ID],[F组织ID],[F创建时间],[F更新时间],[F启用年度],[F启用期间]"
for setid in("1","2"):
    rows,org=assemble(setid)
    buf.write(f"-- ===== 账套{setid} (组织ID={org})  共{len(rows)}科目 (其中现有1002/1012={len(keep[setid])}) =====\n")
    for r in rows:
        v=[r["_fid"],r["code"],r["name"],r["cat"],r["dir"],r["lvl"],r["_pid"],r["leaf"],r["aux"],
           None,None,1,int(setid),int(org),"GETDATE()","GETDATE()",0,0]
        # 时间用 GETDATE() 不加引号
        vs=[]
        for i,x in enumerate(v):
            if i in(14,15): vs.append("GETDATE()")
            else: vs.append(sqlval(x))
        buf.write(f"INSERT INTO [FIN科目] ({cols}) VALUES ({', '.join(vs)});\n")
    buf.write("\n")
buf.write("COMMIT;\nPRINT '科目覆盖完成';\n")
open("docs/superpowers/specs/2026-06-15-科目覆盖迁移.sql","w",encoding="utf-8").write(buf.getvalue())
# 统计
n1=len(assemble("1")[0]);n2=len(assemble("2")[0])
print(f"账套1={n1}(现有1002/1012={len(keep['1'])}) 账套2={n2}(现有1002/1012={len(keep['2'])}) 公共={len(COMMON)}")
print("org1=%s org2=%s"%(orgof.get('1'),orgof.get('2')))
