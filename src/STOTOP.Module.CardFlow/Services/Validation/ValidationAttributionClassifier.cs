using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Validation;

public class ValidationClassificationInput
{
    public List<string> MissingRequiredFields { get; set; } = [];
    public List<string> ConfigurationIssues { get; set; } = [];
    public bool ConfigurationMatched { get; set; }
    public decimal? ExpectedValue { get; set; }
    public decimal? SystemValue { get; set; }
    public decimal Tolerance { get; set; } = 0.01m;
    public bool PersistedResultExists { get; set; }
}

public class ValidationClassificationResult
{
    public ValidationAttribution Attribution { get; set; }
    public ValidationSeverity Severity { get; set; }
    public decimal Confidence { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
}

public class ValidationAttributionClassifier
{
    public ValidationClassificationResult Classify(ValidationClassificationInput input)
    {
        if (input.MissingRequiredFields.Count > 0)
        {
            return new ValidationClassificationResult
            {
                Attribution = ValidationAttribution.ImportData,
                Severity = ValidationSeverity.Blocker,
                Confidence = 0.95m,
                SuggestedAction = $"请先修正导入字段：{string.Join("、", input.MissingRequiredFields)}"
            };
        }

        if (!input.ConfigurationMatched || input.ConfigurationIssues.Count > 0)
        {
            return new ValidationClassificationResult
            {
                Attribution = ValidationAttribution.Configuration,
                Severity = ValidationSeverity.High,
                Confidence = 0.9m,
                SuggestedAction = $"请检查业务配置：{string.Join("；", input.ConfigurationIssues.DefaultIfEmpty("未命中必要配置"))}"
            };
        }

        if (input.ExpectedValue.HasValue && !input.PersistedResultExists)
        {
            return new ValidationClassificationResult
            {
                Attribution = ValidationAttribution.Persistence,
                Severity = ValidationSeverity.High,
                Confidence = 0.85m,
                SuggestedAction = "解释计算已有结果，但实际业务结果缺失，请检查生成或写入链路。"
            };
        }

        if (input.ExpectedValue.HasValue && input.SystemValue.HasValue)
        {
            var diff = Math.Abs(input.ExpectedValue.Value - input.SystemValue.Value);
            if (diff > input.Tolerance)
            {
                return new ValidationClassificationResult
                {
                    Attribution = ValidationAttribution.CalculationLogic,
                    Severity = ValidationSeverity.High,
                    Confidence = 0.8m,
                    SuggestedAction = $"系统值 {input.SystemValue.Value} 与解释值 {input.ExpectedValue.Value} 不一致，请检查计算逻辑或结果回写。"
                };
            }
        }

        return new ValidationClassificationResult
        {
            Attribution = ValidationAttribution.None,
            Severity = ValidationSeverity.Low,
            Confidence = 1m,
            SuggestedAction = "未发现异常。"
        };
    }
}
