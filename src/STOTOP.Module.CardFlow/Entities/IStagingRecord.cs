using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public interface IStagingRecord : IOrgScoped
{
    long FID { get; set; }
    long F批次ID { get; set; }
    int F处理状态 { get; set; }
    string? F错误信息 { get; set; }
    long? F关联凭证ID { get; set; }
    DateTime F创建时间 { get; set; }
    string? FDataScopeId { get; set; }
    long? FSourceWorkItemId { get; set; }
    bool FIsRevoked { get; set; }
    long? F账套ID { get; set; }
    string? F归属网点编号 { get; set; }
}
