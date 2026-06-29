using System.Reflection;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services;
using Xunit;

namespace STOTOP.Module.Express.Tests.Quotation;

public class QuotationDtoMappingTests
{
    [Fact]
    public void MapToDto_returns_segment_ids_that_match_price_cell_segment_ids()
    {
        var matrix = new PricingMatrix
        {
            Segments =
            [
                new PricingSegment
                {
                    SegmentIndex = 1,
                    WeightFrom = 0m,
                    WeightTo = 1m,
                    RoundingMethod = 1,
                    Cells =
                    [
                        new PricingCell
                        {
                            ProvinceId = 1,
                            BasePrice = 4m,
                            FirstWeight = 0m,
                            ContinuePrice = 0m,
                            ContinueStep = 1m
                        }
                    ]
                },
                new PricingSegment
                {
                    SegmentIndex = 2,
                    WeightFrom = 1m,
                    WeightTo = 2m,
                    RoundingMethod = 1,
                    Cells =
                    [
                        new PricingCell
                        {
                            ProvinceId = 1,
                            BasePrice = 5m,
                            FirstWeight = 1m,
                            ContinuePrice = 2m,
                            ContinueStep = 0.5m
                        }
                    ]
                }
            ]
        };

        var dto = InvokeMapToDto(new ExpQuotation
        {
            FID = 1001,
            FBrandCode = "STO",
            FPlanName = "测试报价",
            FSettlementWeightStage = 1,
            FMatrixJson = PricingMatrixSerializer.Serialize(matrix)
        });

        var segmentIds = dto.Segments.Select(s => s.Id).ToHashSet();

        Assert.Equal([1L, 2L], dto.Segments.Select(s => s.Id).ToArray());
        Assert.All(dto.Cells, cell => Assert.Contains(cell.SegmentId, segmentIds));
        Assert.Contains(dto.Cells, cell => cell.SegmentId == 1 && cell.ProvinceId == 1 && cell.BasePrice == 4m);
        Assert.Contains(dto.Cells, cell => cell.SegmentId == 2 && cell.ProvinceId == 1 && cell.BasePrice == 5m);
    }

    [Fact]
    public void MapToDto_returns_price_cell_fields_read_by_frontend_matrix()
    {
        var matrix = new PricingMatrix
        {
            Segments =
            [
                new PricingSegment
                {
                    SegmentIndex = 1,
                    WeightFrom = 0m,
                    WeightTo = 1m,
                    RoundingMethod = 1,
                    Cells =
                    [
                        new PricingCell
                        {
                            ProvinceId = 1,
                            BasePrice = 4m,
                            FirstWeight = 0.5m,
                            ContinuePrice = 1.2m,
                            ContinueStep = 0.5m,
                            TruncParamOverride = 0.1m,
                            CeilParamOverride = 0.5m
                        }
                    ]
                }
            ]
        };

        var dto = InvokeMapToDto(new ExpQuotation
        {
            FID = 1002,
            FBrandCode = "STO",
            FPlanName = "测试报价",
            FSettlementWeightStage = 1,
            FMatrixJson = PricingMatrixSerializer.Serialize(matrix)
        });

        var cell = Assert.Single(dto.Cells);

        Assert.Equal(0.5m, GetNullableDecimalProperty(cell, "FirstWeight"));
        Assert.Equal(0.5m, GetNullableDecimalProperty(cell, "ContinueStep"));
        Assert.Equal(0.1m, GetNullableDecimalProperty(cell, "TruncParamOverride"));
        Assert.Equal(0.5m, GetNullableDecimalProperty(cell, "CeilParamOverride"));
    }

    private static QuotationDto InvokeMapToDto(ExpQuotation quotation)
    {
        var method = typeof(QuotationService).GetMethod(
            "MapToDto",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(ExpQuotation), typeof(string)],
            modifiers: null);

        Assert.NotNull(method);
        var dto = method!.Invoke(null, [quotation, null]);
        return Assert.IsType<QuotationDto>(dto);
    }

    private static decimal? GetNullableDecimalProperty(PriceCellDto cell, string propertyName)
    {
        var property = typeof(PriceCellDto).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        var value = property!.GetValue(cell);
        return value switch
        {
            null => null,
            decimal number => number,
            _ => throw new Xunit.Sdk.XunitException(
                $"Expected {propertyName} to be decimal? but got {value.GetType().Name}.")
        };
    }
}
