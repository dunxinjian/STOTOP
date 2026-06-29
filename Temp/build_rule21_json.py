#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""规则21 品牌版重配工具 (Task 2)。

读 v6 映射 (Temp/rule21_mapping_v6.json) 的 combos[101 损益]，
现查账套2 的 code->fid (dbq)，生成品牌版规则21 的 RulesBasedVoucherConfigV2
JSON (101 个 ruleGroup)，写 Temp/rule21_brand_config.json。

关键点：
- target.fid 一律忽略，改用 dbq 现查的 code2fid[target.code] (Task1 刚改过科目表)。
- 借方 220201 总部应付：走 code2fid 反查并断言 == 700125。
- 辅助核算 3 维：outlet(动态)/express_brand(fixed 16/ST)/business_direction(fixed 组值)；
  借方行只配 express_brand。
- gapFund(3 资金类) 不进规则。
"""
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMP = os.path.join(ROOT, "Temp")
MAPPING_FILE = os.path.join(TEMP, "rule21_mapping_v6.json")
DBQ = os.path.join(TEMP, "dbq.py")
CODES_DUMP = os.path.join(TEMP, "_acct2_codes.json")
OUT_FILE = os.path.join(TEMP, "rule21_brand_config.json")

# controller 已确认的固定参数
BORROW_CODE = "220201"           # 借方一律 总部应付
BORROW_FID_EXPECT = 700125       # 断言值
STAGING_TABLE = "STG申通总部交易明细"
DATE_FIELD = "F业务日期"
INCOME_AMOUNT_FIELD = "F发生额收入"   # 收入侧(总部应付)
COST_AMOUNT_FIELD = "F发生额支出"     # 成本侧(总部应收)
ACCOUNT_SET_ID = 2
SUMMARY_FIELD = "F业务摘要"
SUMMARY_TEMPLATE = "{F业务摘要}"

# express_brand 固定项(账套0全局项 申通)
BRAND_ITEM_ID = 16
BRAND_ITEM_CODE = "ST"


def load_code2fid():
    """现查账套2 全部 code->fid。每次无条件重查 dbq（Task1 刚改过科目表，不能复用旧缓存）。"""
    subprocess.run(
        [sys.executable, DBQ,
         "SELECT FID,F编码 FROM FIN科目 WHERE F账套ID=2",
         "--out", CODES_DUMP],
        check=True,
    )
    with open(CODES_DUMP, encoding="utf-8") as f:
        rsets = json.load(f)
    rs = rsets[0]
    ci_fid = rs["columns"].index("FID")
    ci_code = rs["columns"].index("F编码")
    return {str(r[ci_code]): int(r[ci_fid]) for r in rs["rows"]}


def aux_brand():
    return {
        "auxType": "express_brand",
        "sourceType": "fixed",
        "fixedItemId": BRAND_ITEM_ID,
        "fixedItemCode": BRAND_ITEM_CODE,
        "fixedValue": None,
        "sourceField": None,
        # 后端 AuxiliaryConfigV2.MatchBy 为不可空 string(默认 "code")，fixed 分支
        # (ResolveFixed) 不读此字段，但模型绑定要求非空，故填默认值 "code"。
        "matchBy": "code",
    }


def aux_outlet():
    return {
        "auxType": "outlet",
        "sourceType": "dynamic",
        "fixedItemId": None,
        "fixedItemCode": None,
        "fixedValue": None,
        "sourceField": "F网点编号",
        "matchBy": "code",
    }


def aux_business_direction(value):
    return {
        "auxType": "business_direction",
        "sourceType": "fixed",
        "fixedItemId": None,
        "fixedItemCode": None,
        "fixedValue": value,
        "sourceField": None,
        # 同上：fixed 不读 MatchBy，填默认值满足非空模型绑定。
        "matchBy": "code",
    }


def build_group(idx, combo, code2fid, borrow_fid):
    code = combo["target"]["code"]
    fid = code2fid[code]
    pay_type = combo["payType"]
    biz_dir = combo["businessDirection"]
    fee_code = combo["feeCode"]
    fee_name = combo["feeName"]

    # 损益行 3 维辅助核算
    pl_aux = [aux_outlet(), aux_brand(), aux_business_direction(biz_dir)]
    # 借方 220201 行只配 express_brand
    borrow_aux = [aux_brand()]

    if pay_type == "总部应付":
        # 收入侧：line1 贷 损益科目；line2 借 220201；金额取 F发生额收入
        amount_field = INCOME_AMOUNT_FIELD
        line1 = {
            "lineNo": 1, "direction": "贷",
            "accountId": fid, "accountCode": code,
            "amountField": amount_field, "summaryTemplate": SUMMARY_TEMPLATE,
            "status": 1, "displayOrder": 1, "auxiliaryConfigs": pl_aux,
        }
        line2 = {
            "lineNo": 2, "direction": "借",
            "accountId": borrow_fid, "accountCode": BORROW_CODE,
            "amountField": amount_field, "summaryTemplate": SUMMARY_TEMPLATE,
            "status": 1, "displayOrder": 2, "auxiliaryConfigs": borrow_aux,
        }
    elif pay_type == "总部应收":
        # 成本侧：line1 借 损益科目；line2 贷 220201；金额取 F发生额支出
        amount_field = COST_AMOUNT_FIELD
        line1 = {
            "lineNo": 1, "direction": "借",
            "accountId": fid, "accountCode": code,
            "amountField": amount_field, "summaryTemplate": SUMMARY_TEMPLATE,
            "status": 1, "displayOrder": 1, "auxiliaryConfigs": pl_aux,
        }
        line2 = {
            "lineNo": 2, "direction": "贷",
            "accountId": borrow_fid, "accountCode": BORROW_CODE,
            "amountField": amount_field, "summaryTemplate": SUMMARY_TEMPLATE,
            "status": 1, "displayOrder": 2, "auxiliaryConfigs": borrow_aux,
        }
    else:
        raise ValueError(f"未知 payType={pay_type} (feeCode={fee_code})")

    return {
        "id": f"g{idx:03d}",
        "name": f"{fee_code} {fee_name}",
        "order": idx,
        "exactCodes": [fee_code],
        "exactCategories": None,
        "categoryKeywords": None,
        "summaryKeywords": None,
        "fallthrough": False,
        "amountAggregation": "ROW",
        "lines": [line1, line2],
    }


def main():
    code2fid = load_code2fid()

    # 借方 220201 反查 + 断言
    if BORROW_CODE not in code2fid:
        raise SystemExit(f"借方科目 {BORROW_CODE} 在账套2 未查到 fid")
    borrow_fid = code2fid[BORROW_CODE]
    if borrow_fid != BORROW_FID_EXPECT:
        raise SystemExit(f"220201 fid 漂移: {borrow_fid} != {BORROW_FID_EXPECT}")

    mapping = json.load(open(MAPPING_FILE, encoding="utf-8"))
    combos = mapping["combos"]
    assert len(combos) == 101, f"combos 数={len(combos)} != 101"

    # 断言所有 target.code 都能查到 fid
    missing = [(c["feeCode"], c["target"]["code"]) for c in combos
               if c["target"]["code"] not in code2fid]
    if missing:
        raise SystemExit("以下 target.code 在账套2 查不到 fid: " + str(missing))

    groups = [build_group(i + 1, c, code2fid, borrow_fid)
              for i, c in enumerate(combos)]

    # exactCodes 跨组应 0 重复(一码一名)
    all_codes = [code for g in groups for code in g["exactCodes"]]
    dups = sorted({c for c in all_codes if all_codes.count(c) > 1})
    if dups:
        raise SystemExit("exactCodes 跨组重复: " + str(dups))

    config = {
        "mode": "rulesBased",
        "version": 2,
        "voucherWord": "记",
        "dateField": DATE_FIELD,
        "stagingTable": STAGING_TABLE,
        "accountSetId": ACCOUNT_SET_ID,
        "keyFields": ["F科目编码", "F费用名称", "F业务摘要"],
        "unmatchedAction": "createDraft",
        "draftPlaceholderAccountId": 700044,  # 1901 待处理财产损溢(账套2)，createDraft 待补录草稿占位科目
        "filterConditions": [],
        "matchingLayers": {
            "exactMatchField": "F科目编码",
            "categoryField": "F费用名称",
            "summaryField": "F业务摘要",
        },
        "ruleGroups": groups,
    }

    with open(OUT_FILE, "w", encoding="utf-8") as f:
        json.dump(config, f, ensure_ascii=False, indent=2)

    print(f"OK -> {OUT_FILE}")
    print(f"groups: {len(groups)}")
    print(f"220201 fid: {borrow_fid} (assert == {BORROW_FID_EXPECT} PASS)")
    print(f"missing target.code: {len(missing)}")
    print(f"exactCodes 跨组重复: {len(dups)}")


if __name__ == "__main__":
    main()
