using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers AFTER builder is created
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
});

//configure ASP.NET Core's session support.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    //options.Cookie.MaxAge = TimeSpan.FromDays(30);
});

//MongoDB 
builder.Services.AddSingleton<MongoDBContext>();

// Email services
builder.Services.AddSingleton<EmailQueue>(); //Creates one shared queue instance for the application.

// Actual SMTP sender
builder.Services.AddScoped<SmtpEmailService>(); //Creates an SMTP sender instance within a dependency-injection scope

// New Brevo API service
builder.Services.AddHttpClient<BrevoEmailService>();

// Controllers use the queue-backed email service
builder.Services.AddScoped<IEmailService, QueuedEmailService>(); //When a controller requests IEmailService, ASP.NET Core provides QueuedEmailService

// Background workers
builder.Services.AddHostedService<EmailQueueWorker>(); //Starts the background service that reads and processes queued jobs.
builder.Services.AddHostedService<RecoveryCodeCleanupService>(); 
builder.Services.AddHostedService<AppointmentReminderService>();

builder.Services.AddScoped<UpcomingAppointmentReminderService>();
builder.Services.AddHostedService<UpcomingAppointmentReminderBackgroundService>();

//Pasword hashing service
builder.Services.AddScoped<IPasswordHasher<Login>, PasswordHasher<Login>>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession(); //enables session middleware for requests

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");

// Public endpoint for the cron scheduler
app.MapGet("/health", () => //added
{
    return Results.Ok(new
    {
        status = "Healthy",
        application = "TAMS",
        timestamp = DateTime.UtcNow
    });
});

app.Run();
