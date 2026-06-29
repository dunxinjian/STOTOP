using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpNetworkPointAliasConfiguration : IEntityTypeConfiguration<ExpNetworkPointAlias>
{
    public void Configure(EntityTypeBuilder<ExpNetworkPointAlias> builder)
    {
        builder.ToTable("EXP快递网点名称映射");

        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FNetworkPointCode).HasColumnName("F网点编号").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => e.FNetworkPointCode).HasDatabaseName("IX_快递网点名称映射_网点编号");
        builder.HasIndex(e => new { e.FName, e.FOrgId }).IsUnique().HasDatabaseName("UQ_快递网点名称映射_名称组织");
    }
}
