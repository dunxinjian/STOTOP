using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlShentongProblemDictConfiguration : IEntityTypeConfiguration<QlShentongProblemDict>
{
    public void Configure(EntityTypeBuilder<QlShentongProblemDict> builder)
    {
        builder.ToTable("QL申通_质量问题字典");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.F承运商).HasColumnName("F承运商").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F质量域).HasColumnName("F质量域").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F来源问题类型原文).HasColumnName("F来源问题类型原文").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F问题类型编码).HasColumnName("F问题类型编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F问题类型名称).HasColumnName("F问题类型名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.F问题大类).HasColumnName("F问题大类").HasMaxLength(100);
        builder.Property(e => e.F问题小类).HasColumnName("F问题小类").HasMaxLength(100);
        builder.Property(e => e.F默认严重度).HasColumnName("F默认严重度");
        builder.Property(e => e.F是否考核).HasColumnName("F是否考核");
        builder.Property(e => e.F是否可归责到人).HasColumnName("F是否可归责到人");
        builder.Property(e => e.F默认整改时限小时).HasColumnName("F默认整改时限小时");
        builder.Property(e => e.F状态).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.F备注).HasColumnName("F备注").HasMaxLength(500);

        builder.HasIndex(e => new { e.FOrgId, e.F承运商, e.F质量域, e.F来源问题类型原文 })
            .IsUnique()
            .HasDatabaseName("UX_QL申通_质量问题字典_来源原文");
    }
}
