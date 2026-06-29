using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Validation;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Validation;

public class ValidationAttributionClassifierTests
{
    [Fact]
    public void Classify_returns_import_data_when_required_field_missing()
    {
        var classifier = new ValidationAttributionClassifier();
        var result = classifier.Classify(new ValidationClassificationInput
        {
            MissingRequiredFields = ["F重量"],
            ConfigurationMatched = false,
            ExpectedValue = null,
            SystemValue = null,
            PersistedResultExists = false
        });

        Assert.Equal(ValidationAttribution.ImportData, result.Attribution);
        Assert.Equal(ValidationSeverity.Blocker, result.Severity);
        Assert.Contains("F重量", result.SuggestedAction);
    }

    [Fact]
    public void Classify_returns_configuration_when_input_exists_but_config_missing()
    {
        var classifier = new ValidationAttributionClassifier();
        var result = classifier.Classify(new ValidationClassificationInput
        {
            MissingRequiredFields = [],
            ConfigurationMatched = false,
            ConfigurationIssues = ["未命中报价方案"],
            ExpectedValue = null,
            SystemValue = null,
            PersistedResultExists = false
        });

        Assert.Equal(ValidationAttribution.Configuration, result.Attribution);
        Assert.Equal(ValidationSeverity.High, result.Severity);
        Assert.Contains("配置", result.SuggestedAction);
    }

    [Fact]
    public void Classify_returns_calculation_logic_when_values_differ()
    {
        var classifier = new ValidationAttributionClassifier();
        var result = classifier.Classify(new ValidationClassificationInput
        {
            MissingRequiredFields = [],
            ConfigurationMatched = true,
            ExpectedValue = 123.45m,
            SystemValue = 120.00m,
            Tolerance = 0.01m,
            PersistedResultExists = true
        });

        Assert.Equal(ValidationAttribution.CalculationLogic, result.Attribution);
        Assert.Equal(ValidationSeverity.High, result.Severity);
        Assert.Contains("系统值", result.SuggestedAction);
    }

    [Fact]
    public void Classify_returns_persistence_when_expected_exists_but_persisted_result_missing()
    {
        var classifier = new ValidationAttributionClassifier();
        var result = classifier.Classify(new ValidationClassificationInput
        {
            MissingRequiredFields = [],
            ConfigurationMatched = true,
            ExpectedValue = 88.00m,
            SystemValue = null,
            Tolerance = 0.01m,
            PersistedResultExists = false
        });

        Assert.Equal(ValidationAttribution.Persistence, result.Attribution);
        Assert.Equal(ValidationSeverity.High, result.Severity);
        Assert.Contains("写入", result.SuggestedAction);
    }
}
