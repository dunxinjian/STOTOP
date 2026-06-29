namespace STOTOP.Module.Finance.Dtos;

public class AuxiliaryTypeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Scope { get; set; } = "org_scoped";
}

public class AuxiliaryItemDto
{
    public long Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    // 扩展字段
    public long AccountSetId { get; set; }
    public string? AuxType { get; set; }
    public string? ShortName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public int EnableStatus { get; set; } = 1;

    // 来源字段
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
}

// 账套维度辅助核算项查询请求
public class AuxiliaryItemQueryRequest
{
    public long AccountSetId { get; set; }
    public string? AuxType { get; set; }  // 筛选类型
    public string? Keyword { get; set; }  // 搜索关键词
}

// 创建账套维度辅助核算项请求
public class AuxiliaryItemCreateRequest
{
    public long AccountSetId { get; set; }
    public string AuxType { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
}

// 从组织架构添加辅助核算项目（部门类）
public class AddFromOrgRequest
{
    public List<long> OrgIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

// 从用户添加辅助核算项目（职员类）
public class AddFromUserRequest
{
    public List<long> UserIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

// 可选部门 DTO
public class AvailableDepartmentDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

// 可选员工 DTO
public class AvailableEmployeeDto
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Account { get; set; }
    public string? Phone { get; set; }
}

// 从CRM客户添加辅助核算项目
public class AddFromCustomerRequest
{
    public List<long> CustomerIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

// 从供应商添加辅助核算项目
public class AddFromSupplierRequest
{
    public List<long> SupplierIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

// 从快递品牌添加辅助核算项目
public class AddFromBrandRequest
{
    public List<long> BrandIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

// 可选客户 DTO
public class AvailableCustomerDto
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Phone { get; set; }
}

// 可选供应商 DTO
public class AvailableSupplierDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Contact { get; set; }
}

// 可选快递品牌 DTO
public class AvailableBrandDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// 可选网点 DTO
public class AvailableNetworkPointDto
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Manager { get; set; }
    public string? Phone { get; set; }
}

// 从网点添加辅助核算项目
public class AddFromNetworkPointRequest
{
    public List<string> NetworkPointCodes { get; set; } = new();
    public long AccountSetId { get; set; }
}
