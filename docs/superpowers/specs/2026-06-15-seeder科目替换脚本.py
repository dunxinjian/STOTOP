# -*- coding: utf-8 -*-
import csv, re, io
import json, base64
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
import pymssql

SEEDER="src/STOTOP.WebAPI/Data/Seeders/FinanceSeeder.cs"
CHART="docs/superpowers/specs/2026-06-15-新科目表.csv"
TS="2026-06-15 00:00:00.000"

def _db():
    rec = json.load(open(r'E:/STOTOP_Fable/src/STOTOP.WebAPI/db-connections.json', encoding='utf-8'))[0]
    K = "STOTOP@2024!DefaultKey32Bytes!"; key = (K.ljust(32,'0')[:32]).encode(); iv = key[:16]
    raw = base64.b64decode(rec['password']); d = Cipher(algorithms.AES(key), modes.CBC(iv)).decryptor()
    pb = d.update(raw)+d.finalize(); up = padding.PKCS7(128).unpadder(); pwd = (up.update(pb)+up.finalize()).decode()
    return pymssql.connect(server=rec['server'], user=rec['username'], password=pwd,
                           database=rec['databaseName'], charset='UTF-8', timeout=40, autocommit=True)

AUX={"网点":"outlet","业务方向":"business_direction","快递品牌":"express_brand","经营单元":"business_unit",
     "客户":"customer","供应商":"supplier","部门":"department","员工":"employee","项目":"project","outlet":"outlet"}
def tr_aux(s):
    if not s or s.strip() in("","_"): return None
    parts=[AUX.get(p.strip(),p.strip()) for p in s.split(",") if p.strip()]
    return ",".join(dict.fromkeys(parts)) if parts else None

# 新科目表(排除1002*/1012*)
COMMON=[]
with open(CHART,encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        c=r["F编码"]
        if c.startswith("1002") or c.startswith("1012"): continue
        COMMON.append(dict(code=c,name=r["F名称"],cat=r["F类别"],dir=r["F余额方向"],
                           lvl=int(r["F级次"]),parent=r["F父编码"],leaf=int(r["F是否末级"]),aux=tr_aux(r["F辅助核算"])))

# 改动1：从现有库读取各账套真实 1002/1012 科目（替换原来从 FinanceSeeder.cs 文本正则提取的逻辑）
keep = {"1": [], "2": []}
_cur = _db().cursor()
for setid in ("1", "2"):
    _cur.execute("SELECT F编码,F名称,F类别,F余额方向,F级次,F是否末级,F辅助核算 FROM [FIN科目] "
                 "WHERE F账套ID=" + setid + " AND (F编码 LIKE '1002%' OR F编码 LIKE '1012%') ORDER BY F编码")
    for code, name, cat, dr, lvl, leaf, aux in _cur.fetchall():
        keep[setid].append(dict(code=code, name=name, cat=cat, dir=dr, lvl=int(lvl),
                                parent=(code[:-2] if len(code) > 4 else ""), leaf=int(leaf), aux=aux))

FID0={"1":600000,"2":700000}
# 改动2：去掉 org/orgof，assemble 只返回 rows
def assemble(setid):
    rows=sorted(keep[setid]+COMMON,key=lambda r:(len(r["code"]),r["code"]))
    code2fid={};fid=FID0[setid]
    for r in rows: fid+=1;code2fid[r["code"]]=fid;r["_fid"]=fid
    for r in rows: r["_pid"]=code2fid.get(r["parent"],0) if r["parent"] else 0
    return rows

def sv(x):
    if x is None:return "NULL"
    if isinstance(x,int):return str(x)
    return "N'"+str(x).replace("'","''")+"'"

# 改动2：cols 去掉 [F组织ID]
cols="[FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]"
body=io.StringIO()
total=0
for setid in("1","2"):
    rows=assemble(setid)
    body.write(f"        -- 账套{setid} 共{len(rows)}科目(含现有1002/1012={len(keep[setid])})\n")
    for r in rows:
        total+=1
        # 改动2：vals 去掉组织ID值（原 {int(org)}, 已删除）
        vals=f"({r['_fid']}, {sv(r['code'])}, {sv(r['name'])}, {sv(r['cat'])}, {sv(r['dir'])}, {r['lvl']}, {r['_pid']}, {r['leaf']}, {sv(r['aux'])}, NULL, NULL, 1, {int(setid)}, N'{TS}', N'{TS}', 0, 0)"
        body.write(f"        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = {r['_fid']})\n        INSERT INTO [FIN科目] ({cols}) VALUES {vals};\n")

# 改动3：生成完整 C# 方法，写到独立文件，不改 FinanceSeeder.cs
newblock = (
    '    private static void InsertBrandAccounts(STOTOPDbContext ctx)\n'
    '    {\n'
    '        // 品牌版科目(2026-06-18 落库)：两账套品牌版业务科目 + 各账套真实1002/1012；FID 600/700xxx\n'
    '        ExecSql(ctx, @"\n'
    '        SET IDENTITY_INSERT [FIN科目] ON;\n'
    + body.getvalue() +
    '        SET IDENTITY_INSERT [FIN科目] OFF;\n        ");\n'
    '    }\n')

open(r'E:/STOTOP_Fable/Temp/InsertBrandAccounts.cs.txt', 'w', encoding='utf-8').write(newblock)
print("OK 生成 InsertBrandAccounts 方法; 新科目总条数=", total,
      "; 账套1=", len(keep['1'])+len(COMMON), " 账套2=", len(keep['2'])+len(COMMON))
print("下一步(Task6): 1) 把该方法粘进 FinanceSeeder.cs; 2) 把 MigrateV1 的 // FIN科目 ExecSql 块替换为 InsertBrandAccounts(ctx);")
