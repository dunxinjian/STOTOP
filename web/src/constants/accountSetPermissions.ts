/**
 * 账套级权限编码常量
 */
export const AccountSetPermissions = {
  // 凭证
  VoucherView: 'accountset:voucher:view',
  VoucherCreate: 'accountset:voucher:create',
  VoucherEdit: 'accountset:voucher:edit',
  VoucherDelete: 'accountset:voucher:delete',
  VoucherSubmit: 'accountset:voucher:submit',
  VoucherAudit: 'accountset:voucher:audit',
  VoucherUnaudit: 'accountset:voucher:unaudit',
  VoucherPrint: 'accountset:voucher:print',
  // 期间
  PeriodView: 'accountset:period:view',
  PeriodClose: 'accountset:period:close',
  PeriodReopen: 'accountset:period:reopen',
  // 报表
  ReportView: 'accountset:report:view',
  ReportExport: 'accountset:report:export',
  // 科目
  SubjectView: 'accountset:subject:view',
  SubjectEdit: 'accountset:subject:edit',
  // 辅助核算
  AuxiliaryView: 'accountset:auxiliary:view',
  AuxiliaryEdit: 'accountset:auxiliary:edit',
  // 设置
  SettingsView: 'accountset:settings:view',
  SettingsEdit: 'accountset:settings:edit',
  // 出纳日记账
  CashJournalView: 'accountset:cashjournal:view',
  CashJournalEdit: 'accountset:cashjournal:edit',
} as const
