# -*- coding: utf-8 -*-
import re, os, sys, gzip, base64
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'Temp'))
import dbq

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SS = os.path.join(ROOT, 'src', 'STOTOP.WebAPI', 'Data', 'Seeders', 'SystemSeeder.cs')

src = open(SS, encoding='utf-8-sig').read()
# 两行格式：SqlChunkN =\n        "..."
chunk_pat = re.compile(r'(SqlChunk(\d+)\s*=\s*\r?\n\s*")([^"]*)(";\s*)')
chunks = {int(m.group(2)): m.group(3) for m in chunk_pat.finditer(src)}
n = len(chunks)
assert n >= 1, "未找到 SqlChunk 常量"
b64 = ''.join(chunks[i] for i in sorted(chunks))
sql = gzip.decompress(base64.b64decode(b64)).decode('utf-8')

dbfids = {r[0] for r in dbq.run("SELECT FID FROM [SYS功能权限]")[0]['rows']}

removed = []
def drop(m):
    fid = int(re.search(r'VALUES\s*\((\d+)', m.group(0)).group(1))
    if fid in dbfids:
        return m.group(0)
    removed.append(fid)
    return ''
new_sql = re.sub(r'INSERT INTO \[SYS功能权限\][^;]*;', drop, sql)
new_sql = re.sub(r'\n{3,}', '\n\n', new_sql)
print(f"删除 SYS功能权限 行 {len(removed)} 条: {sorted(removed)}")

nb64 = base64.b64encode(gzip.compress(new_sql.encode('utf-8'))).decode('ascii')
size = (len(nb64) + n - 1) // n
parts = [nb64[i*size:(i+1)*size] for i in range(n)]
while len(parts) < n:
    parts.append('')

def repl(m):
    idx = int(m.group(2))
    return f'{m.group(1)}{parts[idx]}{m.group(4)}'
out = chunk_pat.sub(repl, src)
open(SS, 'w', encoding='utf-8').write(out)
print(f"已回写 {n} 块。新 base64 长度 {len(nb64)}（原 {len(b64)}）")
