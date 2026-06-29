# -*- coding: utf-8 -*-
import json, os, sys
from datetime import datetime
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'Temp'))
import dbq  # 复用解密+连库

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
BL = os.path.join(ROOT, 'src', 'STOTOP.WebAPI', 'Data', 'Seeders', 'Baseline', 'baseline-reference-data.json')

d = json.load(open(BL, encoding='utf-8'))
perm = next(t for t in d['tables'] if t['Name'] == 'SYS功能权限')
before = len(perm['Rows'])

# 1) 删 oa:* 行
perm['Rows'] = [r for r in perm['Rows'] if not str(r.get('F编码', '')).startswith('oa:')]
removed = before - len(perm['Rows'])

# 2) 从现库取 624/629/2122 三行(若 baseline 已无)，按 baseline 列对象追加
have = {r['FID'] for r in perm['Rows']}
need = [f for f in (624, 629, 2122) if f not in have]
if need:
    cols = [c['Name'] for c in perm['Columns']]
    collist = ','.join(f'[{c}]' for c in cols)
    rs = dbq.run(f"SELECT {collist} FROM [SYS功能权限] WHERE FID IN ({','.join(map(str,need))})")
    rset = rs[0]
    # 用位置索引映射（dbq 返回列名可能乱码，但列顺序与 SELECT 一致）
    for row in rset['rows']:
        rec = {}
        for i, c in enumerate(cols):
            v = row[i]
            # datetime -> ISO 字符串（与 baseline 现有格式一致）
            if isinstance(v, datetime):
                v = v.isoformat()
            rec[c] = v
        perm['Rows'].append(rec)

# 3) 排序 + 重算 RowCount
perm['Rows'].sort(key=lambda r: r['FID'])
perm['RowCount'] = len(perm['Rows'])

json.dump(d, open(BL, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print(f"删 oa:* {removed} 行；补 {len(need)} 行；SYS功能权限 现 {perm['RowCount']} 行")
