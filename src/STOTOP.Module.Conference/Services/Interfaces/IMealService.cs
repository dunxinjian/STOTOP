using STOTOP.Module.Conference.Dtos;

namespace STOTOP.Module.Conference.Services.Interfaces;

public interface IMealService
{
    Task<List<MealPlanListItemDto>> GetMealPlansAsync(int eventId);
    Task<MealPlanDto?> GetMealPlanByIdAsync(int id);
    Task<MealPlanDto> CreateMealPlanAsync(int eventId, CreateMealPlanRequest request);
    Task<MealPlanDto?> UpdateMealPlanAsync(int id, UpdateMealPlanRequest request);
    Task<bool> DeleteMealPlanAsync(int id);
    Task<MealPlanDto?> SetMealAttendeesAsync(int id, MealAttendeeRequest request);
    Task<List<MealPlanListItemDto>> AutoGenerateMealPlansAsync(int eventId);
}
