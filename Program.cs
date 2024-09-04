using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
builder.Services.AddDbContext<ActionDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Welcome to user action API!");

app.MapPost("/actions/", async (ActionDto actionDto, ActionDbContext Db) =>
{
    var action = new Action()
    {
        DateTime = actionDto.DateTime.ToUniversalTime(),
        Sender = actionDto.Sender,
        Description = actionDto.Description
    };
    Db.Actions.Add(action);
    await Db.SaveChangesAsync();

    return Results.Created($"/actions/{action.Id}", action);
});

app.MapGet("/actions/filters/senders", async ([FromQuery]string Sender, ActionDbContext Db, CancellationToken cancellation) =>
{
    var actions = await Db.Actions.Where(x => x.Sender == Sender).ToListAsync(cancellation);
    return actions.Any() ? Results.Ok(actions) : Results.NotFound("No actions found for this sender.");
});

app.MapGet("/actions/filters/dates", async ([FromQuery]string ActionDateFrom, [FromQuery] string ActionDateTo, ActionDbContext Db, CancellationToken cancellation) =>
{
    if (DateTime.TryParse(ActionDateFrom, out DateTime dateFrom) &&
        DateTime.TryParse(ActionDateTo, out DateTime dateTo))
    {
        if(dateFrom>dateTo) return Results.BadRequest("Invalid date format.");
        var actions = await Db.Actions.Where(x => x.DateTime >= dateFrom.ToUniversalTime() && x.DateTime <= dateTo.ToUniversalTime()).ToListAsync(cancellation);
        return actions.Any() ? Results.Ok(actions) : Results.NotFound("No actions found in this date range.");
    }

    return Results.BadRequest("Invalid date format.");
});

// Read all Actions
app.MapGet("/actions", async (ActionDbContext Db) => await Db.Actions.ToListAsync());

using (var scope = app.Services.CreateScope())
{
    app.Logger.LogInformation("Migration started");
    var dbContext = scope.ServiceProvider.GetRequiredService<ActionDbContext>();
    dbContext.Database.Migrate();
    app.Logger.LogInformation("Migration ended");
}

app.Run();

public record ActionDto(DateTime DateTime, string Sender, string Description);

internal class Action // Исправлено: фигурные скобки вместо круглых
{
    public Guid Id { get; set; }
    public DateTime DateTime { get; set; }
    public string Sender { get; set; } = default!;
    public string Description { get; set; } = default!;
}

internal class ActionConfiguration : IEntityTypeConfiguration<Action>
{
    public void Configure(EntityTypeBuilder<Action> configuration)
    {
        configuration.HasKey(k => k.Id);
        configuration.HasIndex(i => i.Sender);
        configuration.HasIndex(i => i.DateTime);
    }
}

class ActionDbContext : DbContext
{
    public DbSet<Action> Actions => Set<Action>();

    public ActionDbContext(DbContextOptions<ActionDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}