using System;
using System.Collections.Generic;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests;

public class AutoVoucherHandlerCreateDraftTests
{
    [Fact]
    public void BuildDraftRequest_未匹配行_生成待补录草稿请求()
    {
        var config = new RulesBasedVoucherConfigV2 { VoucherWord = "记", UnmatchedAction = "createDraft" };
        var rows = new List<IDictionary<string, object>> {
            new Dictionary<string, object> { ["F发生额支出"] = 60m, ["F发生额收入"] = 0m, ["F业务摘要"] = "未知费用X" },
            new Dictionary<string, object> { ["F发生额收入"] = 40m, ["F发生额支出"] = 0m, ["F业务摘要"] = "未知费用Y" },
        };
        var req = AutoVoucherHandler.BuildDraftRequest(config, rows, new DateTime(2026, 2, 15), periodId: 5, batchId: 1111, scopeId: "1111_unmatched_20260215");
        Assert.Equal("费用支出", req.Source);
        Assert.Contains("[待补录]", req.Remark);
        Assert.Equal(new DateTime(2026, 2, 15), req.Date);
        Assert.Equal(2, req.Entries.Count);
        Assert.Equal(100m, req.Entries[0].DebitAmount);   // 借方=收入列+支出列逐行累加(60+40)
        Assert.Equal(100m, req.Entries[1].CreditAmount);
    }
}
