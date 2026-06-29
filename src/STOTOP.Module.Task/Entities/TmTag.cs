using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTag : BaseEntity, IOrgScoped
{
    public string FName { get; set; } = string.Empty;
    public string FColor { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public int FSort { get; set; } = 0;

    // 导航属性
    public virtual ICollection<TmTaskTag> TaskTags { get; set; } = new List<TmTaskTag>();
}
