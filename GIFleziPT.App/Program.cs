using GIFleziPT.App.Configs;
using GIFleziPT.App.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

AppSettings.Configs = builder.Configuration;
AppSettings.Instance = AppSettings.Configs.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();


// Add services to the container.

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();

    var logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
    loggingBuilder.AddSerilog(logger);
});

builder.Services.AddControllers();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddHostedService<TaskRunnerJob>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
