# -*- coding: utf-8 -*-
"""产出 SQL 转义后的 config 字符串文件(供粘进 CardFlowSeeder MigrateV55/V56 的 N'...' 内)。
依赖:先跑 build_rule3131.py 产出 _rule3131_config.min.json。
C# 逐字字符串 @"..." 规则:只把双引号 " 转成 ""(单引号不转义,大括号不转义)。"""
import json, os

OUTDIR = r"E:\STOTOP_Fable\Temp\gen"
os.makedirs(OUTDIR, exist_ok=True)

def sqlstr(s):
    return s.replace('"', '""')

# ---- 凭证规则3131(大,机器生成) ----
cfg3131 = open(r"E:\STOTOP_Fable\Temp\_rule3131_config.min.json", encoding="utf-8").read()
open(os.path.join(OUTDIR, "rule3131_sqlescaped.txt"), "w", encoding="utf-8").write(sqlstr(cfg3131))

# ---- 导入规则3130(小,也产出+打印供粘贴) ----
rule3130 = {
    "targetTable": "STG申通总部交易明细", "outputMode": "stg", "headerRow": 1, "dataStartRow": 2,
    "columnMapping": [
        {"excelColumn": "会计日期", "dbColumn": "F业务日期"},
        {"excelColumn": "扣付日期", "dbColumn": "F记账日期"},
        {"excelColumn": "网点名称", "dbColumn": "F网点名称"},
        {"excelColumn": "网点编码", "dbColumn": "F网点编号"},
        {"excelColumn": "交易类型", "dbColumn": "F费用类型"},
        {"excelColumn": "费用名称", "dbColumn": "F费用名称"},
        {"excelColumn": "费用编码", "dbColumn": "F科目编码"},
        {"excelColumn": "费用收付类型", "dbColumn": "F费用收付类型"},
        {"excelColumn": "业务摘要", "dbColumn": "F业务摘要"},
        {"excelColumn": "发生额(收入)", "dbColumn": "F发生额收入"},
        {"excelColumn": "发生额(支出)", "dbColumn": "F发生额支出"},
        {"excelColumn": "余额", "dbColumn": "F余额"},
        {"excelColumn": "进/出港标识", "dbColumn": "F进出港标识"},
        {"excelColumn": "关联单号", "dbColumn": "F运单编号"},
    ],
    "decimalFields": ["F发生额收入", "F发生额支出", "F余额"],
    "keyFields": ["会计日期", "网点编码", "业务摘要", "费用名称", "费用编码", "费用收付类型", "发生额(收入)", "发生额(支出)", "余额"],
    "fullColumnIdentifier": "会计日期,扣付日期,结算日期,网点名称,网点编码,交易类型,费用名称,费用编码,费用收付类型,业务摘要,发生额(收入),发生额(支出),余额,进/出港标识,联系人,关联单号,银行账号",
    "columnIdentifier": "会计日期,费用编码,费用收付类型,进/出港标识",
    "totalRowDetection": {"enabled": True, "containsKeywords": ["合计", "总计"], "emptyFields": ["会计日期", "网点编码", "余额"]},
    "crossBatchDedupEnabled": False, "batchSplit": {"enabled": False},
}
rule3130_min = json.dumps(rule3130, ensure_ascii=False, separators=(",", ":"))
open(os.path.join(OUTDIR, "rule3130_sqlescaped.txt"), "w", encoding="utf-8").write(sqlstr(rule3130_min))

print("OK ->", OUTDIR)
print("rule3131 转义后字符:", len(sqlstr(cfg3131)))
print("rule3130 转义后字符:", len(sqlstr(rule3130_min)))
print("\n--- rule3130 (SQL转义后,粘进 MigrateV55 的 N'' 内) ---")
print(sqlstr(rule3130_min))
