using CalendarPlaybook.Api.Features.Events.CreateEvent;

namespace CalendarPlaybook.Api.Features.Events.UpdateEvent;

/// <summary>Input rules for <c>PUT /api/events/{id}</c>: identical to <c>POST</c> (spec sections 4, 5.1 and 9). Error keys are the JSON field paths.</summary>
public static class UpdateEventValidator
{
    /// <summary>Returns the validation errors by field. An empty dictionary means the request is valid.</summary>
    public static Dictionary<string, string[]> Validate(UpdateEventRequest request) =>
        CreateEventValidator.Validate(request.ToCreateRequest());
}
