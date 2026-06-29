# -*- coding: utf-8 -*-
"""种子卫生：把 SystemSeeder gzip 种子 blob 里的真实用户清掉，fresh DB 只种 admin(FID=1)。
- SYS用户：仅保留 FID=1(admin)，移除其余(真实人员+密码哈希)。
- SYS用户角色：移除引用被删用户(F用户ID 或 SysUserFID)的行。
- SYS组织架构：F负责人ID 指向被删用户的，置 NULL(否则 FK_…负责人 在全新库建表失败)。
功能权限/角色/组织架构其余字段不动。仅改 fresh-DB 种子，现行库 500 用户不受影响。
幂等可重跑(二次运行 removed 为空，无改动)。"""
import re, os, gzip, base64

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SS = os.path.join(ROOT, 'src', 'STOTOP.WebAPI', 'Data', 'Seeders', 'SystemSeeder.cs')
KEEP_USER = 1


def split_values(s):
    """按顶层逗号切分 SQL VALUES 内容，尊重 N'...' 字符串(含 '' 转义)。"""
    out, cur, inq, i = [], '', False, 0
    while i < len(s):
        c = s[i]
        if c == "'":
            if inq and i + 1 < len(s) and s[i + 1] == "'":
                cur += "''"; i += 2; continue
            inq = not inq; cur += c; i += 1; continue
        if c == ',' and not inq:
            out.append(cur.strip()); cur = ''; i += 1; continue
        cur += c; i += 1
    out.append(cur.strip())
    return out


def values_of(line):
    m = re.search(r'VALUES\s*\((.*)\)\s*;\s*$', line)
    return split_values(m.group(1)) if m else None


src = open(SS, encoding='utf-8-sig').read()
chunk_pat = re.compile(r'(SqlChunk(\d+)\s*=\s*")([^"]*)(";)')
chunks = {int(m.group(2)): m.group(3) for m in chunk_pat.finditer(src)}
n = len(chunks)
assert n >= 1, "未找到 SqlChunk 常量"
b64 = ''.join(chunks[i] for i in sorted(chunks))
sql = gzip.decompress(base64.b64decode(b64)).decode('utf-8')

# 判定基准 = 最终实际种入的用户集合(只有 admin)。引用集外用户的从属行一律清理/置空，
# 既清掉被删的真实用户(2-500)，也清掉本就悬挂的非 blob 用户(如 1062)。
KEPT = {KEEP_USER}


def kept(v):
    """该值是否为已种用户ID(NULL 视为无引用、放行)。"""
    v = v.strip().upper()
    return v == 'NULL' or (v.isdigit() and int(v) in KEPT)


dropped_users = dropped_roles = nulled_owners = 0
out_lines = []
for line in sql.split('\n'):
    st = line.strip()
    if st.startswith('INSERT INTO [SYS用户]') and 'VALUES' in st:
        if int(values_of(st)[0]) not in KEPT:
            dropped_users += 1
            continue
    elif st.startswith('INSERT INTO [SYS用户角色]') and 'VALUES' in st:
        vals = values_of(st)
        # F用户ID(idx1) 或 SysUserFID(idx5) 引用了非已种用户 → 删行
        if not kept(vals[1]) or not kept(vals[5] if len(vals) > 5 else 'NULL'):
            dropped_roles += 1
            continue
    elif st.startswith('INSERT INTO [SYS组织架构]') and 'VALUES' in st:
        vals = values_of(st)
        if not kept(vals[12]):  # F负责人ID 指向非已种用户 → 置 NULL(否则 FK 失败)
            vals[12] = 'NULL'
            line = re.sub(r'VALUES\s*\(.*\)\s*;\s*$', 'VALUES (' + ', '.join(vals) + ');', line)
            nulled_owners += 1
    out_lines.append(line)

new_sql = '\n'.join(out_lines)
print(f"删用户 {dropped_users} 行(保留admin FID=1)；删用户角色 {dropped_roles} 行；组织负责人置NULL {nulled_owners} 行")

nb64 = base64.b64encode(gzip.compress(new_sql.encode('utf-8'))).decode('ascii')
size = (len(nb64) + n - 1) // n
parts = [nb64[i * size:(i + 1) * size] for i in range(n)]
while len(parts) < n:
    parts.append('')
open(SS, 'w', encoding='utf-8').write(chunk_pat.sub(lambda m: f'{m.group(1)}{parts[int(m.group(2))]}{m.group(4)}', src))
print(f"回写 {n} 块；新base64 {len(nb64)}（原 {len(b64)}）")
