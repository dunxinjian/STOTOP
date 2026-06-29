using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Contract.Entities;

namespace STOTOP.Module.Contract.Configurations;

public class ConContractConfiguration : IEntityTypeConfiguration<ConContract>
{
    public void Configure(EntityTypeBuilder<ConContract> builder)
    {
        builder.ToTable("CON合同");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FContractNo).HasColumnName("F合同号").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FTitle).HasColumnName("F标题").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FTypeId).HasColumnName("F类型ID");
        builder.Property(e => e.FTemplateId).HasColumnName("F模板ID");
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasColumnType("decimal(14,2)");
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期");
        builder.Property(e => e.FEndDate).HasColumnName("F结束日期");
        builder.Property(e => e.FRelatedContractId).HasColumnName("F关联合同ID");
        builder.Property(e => e.FContractNature).HasColumnName("F合同性质").HasDefaultValue(1);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FOaProcessInstanceId).HasColumnName("FOA流程实例ID");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FContractNo).IsUnique().HasDatabaseName("UX_CON合同_F合同号");
        builder.HasIndex(e => e.FTypeId).HasDatabaseName("IX_CON合同_F类型ID");
        builder.HasIndex(e => e.FTemplateId).HasDatabaseName("IX_CON合同_F模板ID");
        builder.HasIndex(e => e.FRelatedContractId).HasDatabaseName("IX_CON合同_F关联合同ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CON合同_F状态");
        builder.HasIndex(e => e.FEndDate).HasDatabaseName("IX_CON合同_F结束日期");

        builder.HasOne(e => e.Type)
            .WithMany()
            .HasForeignKey(e => e.FTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Template)
            .WithMany()
            .HasForeignKey(e => e.FTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RelatedContract)
            .WithMany()
            .HasForeignKey(e => e.FRelatedContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Parties)
            .WithOne(e => e.Contract)
            .HasForeignKey(e => e.FContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Clauses)
            .WithOne(e => e.Contract)
            .HasForeignKey(e => e.FContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Reminders)
            .WithOne(e => e.Contract)
            .HasForeignKey(e => e.FContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ESignRecords)
            .WithOne(e => e.Contract)
            .HasForeignKey(e => e.FContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
