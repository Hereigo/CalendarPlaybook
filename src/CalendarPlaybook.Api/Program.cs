using System.Text.Json.Serialization;
using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Features.Events.DeleteEvent;
using CalendarPlaybook.Api.Features.Events.GetEvent;
using CalendarPlaybook.Api.Features.Events.UpdateEvent;
using CalendarPlaybook.Api.Infrastructure.Auth;
using CalendarPlaybook.Api.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddAuth0(builder.Configuration);
builder.Services.AddCalendarDb(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/events")
    .RequireAuthorization()
    .MapCreateEvent()
    .MapGetEvent()
    .MapUpdateEvent()
    .MapDeleteEvent();

app.MigrateInDevelopment();

app.Run();
