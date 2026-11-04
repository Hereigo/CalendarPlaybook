using System.Text.Json.Serialization;
using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Features.Events.DeleteEvent;
using CalendarPlaybook.Api.Features.Events.GetEvent;
using CalendarPlaybook.Api.Features.Events.ListOccurrences;
using CalendarPlaybook.Api.Features.Events.UpdateEvent;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using CalendarPlaybook.Api.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddCidaas(builder.Configuration);
builder.Services.AddCalendarDb(builder.Configuration);

var spaOrigin = builder.Configuration["Cors:AllowedOrigin"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (!string.IsNullOrWhiteSpace(spaOrigin))
        policy.WithOrigins(spaOrigin).WithMethods("GET", "POST", "PUT", "DELETE").WithHeaders("Authorization", "Content-Type");
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/events")
    .RequireAuthorization()
    .MapCreateEvent()
    .MapGetEvent()
    .MapUpdateEvent()
    .MapDeleteEvent()
    .MapListOccurrences();

if (app.Environment.IsDevelopment())
{
    DevWebServer.Start(app);
}

app.MigrateInDevelopment();

app.Run();
