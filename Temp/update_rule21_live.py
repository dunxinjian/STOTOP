# -*- coding: utf-8 -*-
"""库 live UPDATE 凭证规则21(FID=21)：备份旧JSON入V1备份 + 追平品牌版新配置。"""
import sys, os
sys.path.insert(0, r'E:\STOTOP_Fable\Temp')
import dbq
import pymssql

cfg = dbq.load_conn()
newjson = open(r'E:\STOTOP_Fable\Temp\rule21_brand_config.json', encoding='utf-8').read()
conn = pymssql.connect(server=cfg["server"], port=int(cfg["port"]), user=cfg["user"],
                       password=cfg["password"], database=cfg["database"], charset="UTF-8")
cur = conn.cursor()
cur.execute("SELECT COUNT(*) FROM CF自动插件_规则 WHERE FID=21")
if cur.fetchone()[0] != 1:
    raise SystemExit("FID=21 不存在，停止")
cur.execute("""UPDATE CF自动插件_规则
    SET F规则配置V1备份 = COALESCE(F规则配置V1备份, F规则配置JSON),
        F规则配置JSON = %s, F更新时间 = GETDATE()
    WHERE FID = 21""", (newjson,))
conn.commit()
print("rows affected:", cur.rowcount, " newjson bytes:", len(newjson.encode('utf-8')))
conn.close()
