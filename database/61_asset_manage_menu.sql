-- 新增资产管理菜单项
-- 2026-05-07

SET IDENTITY_INSERT [SYS功能权限] ON;

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [FID]=1854)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见]) 
    VALUES (1854,N'资产类别',N'finance:asset:category',N'菜单',6,N'/finance/asset-categories',N'finance/AssetCategoryManage',N'AppstoreOutlined',16,1,1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [FID]=1855)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见]) 
    VALUES (1855,N'资产卡片',N'finance:asset:card',N'菜单',6,N'/finance/asset-cards',N'finance/AssetCardManage',N'CreditCardOutlined',17,1,1);

SET IDENTITY_INSERT [SYS功能权限] OFF;
