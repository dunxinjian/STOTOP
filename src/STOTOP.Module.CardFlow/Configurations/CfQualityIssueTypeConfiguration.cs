using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class CfQualityIssueTypeConfiguration : IEntityTypeConfiguration<CfQualityIssueType>
{
    public void Configure(EntityTypeBuilder<CfQualityIssueType> builder)
    {
        builder.ToTable("CF质量问题类型");

        builder.Property(e => e.FID).HasColumnName("FID").ValueGeneratedOnAdd();
        builder.Property(e => e.FCode).HasColumnName("FCode").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FName).HasColumnName("FName").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("FDescription").HasMaxLength(500);
        builder.Property(e => e.FModule).HasColumnName("FModule").HasMaxLength(50).IsRequired().HasDefaultValue("Express");
        builder.Property(e => e.FSourceAutoPlugin).HasColumnName("FSourceAutoPlugin").HasMaxLength(100);
        builder.Property(e => e.FSeverityLevel).HasColumnName("FSeverityLevel").HasMaxLength(20).IsRequired().HasDefaultValue("Warning");
        builder.Property(e => e.FCategory).HasColumnName("FCategory").HasMaxLength(50).IsRequired().HasDefaultValue("DataQuality");
        builder.Property(e => e.FIsBuiltIn).HasColumnName("FIsBuiltIn").HasDefaultValue(false);
        builder.Property(e => e.FSuggestedFix).HasColumnName("FSuggestedFix").HasMaxLength(500);
        builder.Property(e => e.FDetailRoute).HasColumnName("FDetailRoute").HasMaxLength(200);
        builder.Property(e => e.FDispatchMode).HasColumnName("FDispatchMode").HasMaxLength(20);
        builder.Property(e => e.FDispatchTarget).HasColumnName("FDispatchTarget").HasColumnType("NVARCHAR(500)");
        builder.Property(e => e.FResolveMode).HasColumnName("FResolveMode").HasMaxLength(30);
        builder.Property(e => e.FCardFlowCode).HasColumnName("FCardFlowCode").HasMaxLength(100);
        builder.Property(e => e.FCardTemplateCode).HasColumnName("FCardTemplateCode").HasMaxLength(100);
        builder.Property(e => e.FActionSchemaJson).HasColumnName("FActionSchemaJson").HasColumnType("NVARCHAR(MAX)");
        builder.Property(e => e.FAfterResolvedAction).HasColumnName("FAfterResolvedAction").HasMaxLength(50);
        builder.Property(e => e.FAggregationMode).HasColumnName("FAggregationMode").HasMaxLength(50).HasDefaultValue("BatchIssue");
        builder.Property(e => e.FOrgScoped).HasColumnName("FOrgScoped").HasDefaultValue(true);
        builder.Property(e => e.FTimeoutHours).HasColumnName("FTimeoutHours").HasDefaultValue(0);
        builder.Property(e => e.FStatus).HasColumnName("FStatus").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode)
            .IsUnique()
            .HasDatabaseName("IX_CF质量问题类型_Code");
    }
}
