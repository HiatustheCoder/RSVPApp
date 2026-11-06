
using SQLite;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5080");

var app = builder.Build();

var databasePath = Path.Combine(
    app.Environment.ContentRootPath,
    "rsvp-server.db3");

var db = new SQLiteAsyncConnection(databasePath);

await db.CreateTableAsync<ApiEvent>();
await db.CreateTableAsync<ApiRsvp>();

app.MapGet("/", () =>
    Results.Ok(new { Message = "RSVP API running" }));

// Retrieve all events
app.MapGet("/api/events", async () =>
{
    var events = await db.Table<ApiEvent>().ToListAsync();
    return Results.Ok(events.OrderBy(e => e.Date));
});

// Create an event
app.MapPost("/api/events", async (ApiEvent item) =>
{
    if (string.IsNullOrWhiteSpace(item.Name) ||
        string.IsNullOrWhiteSpace(item.Address) ||
        item.Date <= DateTime.Now ||
        item.Capacity < 1)
    {
        return Results.BadRequest("Invalid event information.");
    }

    var newEvent = new ApiEvent
    {
        Name = item.Name.Trim(),
        Address = item.Address.Trim(),
        Description = item.Description?.Trim() ?? "",
        Date = item.Date,
        Capacity = item.Capacity
    };

    await db.InsertAsync(newEvent);

    return Results.Created(
        $"/api/events/{newEvent.Id}", newEvent);
});

// Retrieve one event
app.MapGet("/api/events/{id:int}", async (int id) =>
{
    var item = await db.FindAsync<ApiEvent>(id);

    return item == null
        ? Results.NotFound()
        : Results.Ok(item);
});

// Retrieve RSVPs for an event
app.MapGet("/api/events/{id:int}/rsvps",
    async (int id) =>
    {
        var records = await db.Table<ApiRsvp>()
            .Where(r => r.EventId == id)
            .ToListAsync();

        return Results.Ok(records);
    });

// Submit RSVP
app.MapPost("/api/events/{id:int}/rsvps",
    async (int id, ApiRsvp request) =>
    {
        var ev = await db.FindAsync<ApiEvent>(id);

        if (ev == null)
            return Results.NotFound("Event not found.");

        if (ev.Date <= DateTime.Now)
            return Results.BadRequest("Event has ended.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Name is required.");

        var accepted = await db.Table<ApiRsvp>()
            .Where(r => r.EventId == id &&
                        r.Status == "Accepted")
            .CountAsync();

        var attendeeName = request.Name.Trim();

        var existing = await db.Table<ApiRsvp>()
            .Where(r => r.EventId == id &&
                        r.Name == attendeeName)
            .FirstOrDefaultAsync();

        if (existing != null)
            return Results.Conflict("RSVP already exists.");

        if (accepted >= ev.Capacity)
            return Results.BadRequest("Event is full.");

        var record = new ApiRsvp
        {
            EventId = id,
            Name = request.Name.Trim(),
            Status = "Accepted"
        };

        await db.InsertAsync(record);

        return Results.Created(
            $"/api/events/{id}/rsvps", record);
    });

app.Run();

[Table("Events")]
public class ApiEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public int Capacity { get; set; }
}

[Table("RSVPs")]
public class ApiRsvp
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EventId { get; set; }

    public string Name { get; set; } = "";
    public string Status { get; set; } = "Accepted";
}
