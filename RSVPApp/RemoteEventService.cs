
using System.Net.Http.Json;

namespace RSVPApp;

public class RemoteEvent
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public int Capacity { get; set; }
}

public class RemoteRsvp
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Accepted";
}

public static class RemoteEventService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(
#if ANDROID
            "http://10.0.2.2:5080/"
#else
            "http://localhost:5080/"
#endif
        ),
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Retrieve events from the API
    public static async Task<List<RemoteEvent>> GetEvents()
    {
        return await Client.GetFromJsonAsync<List<RemoteEvent>>(
            "api/events") ?? new();
    }

    // Save a new event to the remote database
    public static async Task<RemoteEvent> CreateEvent(
        RemoteEvent item)
    {
        var response = await Client.PostAsJsonAsync(
            "api/events", item);

        await EnsureSuccess(response);

        return (await response.Content
            .ReadFromJsonAsync<RemoteEvent>())!;
    }

    // Retrieve RSVP responses for an event
    public static async Task<List<RemoteRsvp>> GetRsvps(
        int eventId)
    {
        return await Client.GetFromJsonAsync<List<RemoteRsvp>>(
            $"api/events/{eventId}/rsvps") ?? new();
    }

    // Submit a new RSVP response
    public static async Task SaveRsvp(
        int eventId, string name)
    {
        var response = await Client.PostAsJsonAsync(
            $"api/events/{eventId}/rsvps",
            new RemoteRsvp
            {
                EventId = eventId,
                Name = name.Trim(),
                Status = "Accepted"
            });

        await EnsureSuccess(response);
    }

    private static async Task EnsureSuccess(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = await response.Content
            .ReadAsStringAsync();

        throw new HttpRequestException(message);
    }
}
