
namespace RSVPApp;

// Displays events stored on the remote Web API.
public class RemoteEventsPage : ContentPage
{
    private readonly VerticalStackLayout list =
        new() { Spacing = 12 };

    public RemoteEventsPage()
    {
        Title = "Remote Events";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Remote RSVP Events"));

        stack.Children.Add(
            Ui.Text("Events stored on the Web API server."));

        stack.Children.Add(
            Ui.Button("Refresh Events", Refresh));

        stack.Children.Add(
            Ui.Button("Create Remote Event", async () =>
            {
                await Navigation.PushAsync(
                    new RemoteAddEventPage());
            }, true));

        stack.Children.Add(list);

        Content = Ui.View(stack);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Refresh();
    }

    private async Task Refresh()
    {
        list.Children.Clear();

        try
        {
            var events =
                await RemoteEventService.GetEvents();

            if (events.Count == 0)
            {
                list.Children.Add(
                    Ui.Text("No remote events found."));
                return;
            }

            foreach (var ev in events)
            {
                var card = new VerticalStackLayout
                {
                    Spacing = 8,
                    Padding = 12,
                    BackgroundColor = Colors.White
                };

                card.Children.Add(
                    Ui.Heading(ev.Name));

                card.Children.Add(
                    Ui.Text(ev.Address));

                card.Children.Add(
                    Ui.Text(ev.Date.ToString(
                        "MMM d, yyyy h:mm tt")));

                card.Children.Add(
                    Ui.Button("View / RSVP", async () =>
                    {
                        await Navigation.PushAsync(
                            new RemoteEventDetailsPage(ev));
                    }));

                list.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            list.Children.Add(
                Ui.Text("API unavailable: " + ex.Message));
        }
    }
}

// Creates a new event on the remote server.
public class RemoteAddEventPage : ContentPage
{
    public RemoteAddEventPage()
    {
        Title = "Create Remote Event";

        var stack = Ui.Stack();

        var name = Ui.Entry("Event name");
        var address = Ui.Entry("Address");
        var description = Ui.Entry("Description");
        var capacity = Ui.Entry("Capacity");

        capacity.Keyboard = Keyboard.Numeric;

        var date = new DatePicker
        {
            Date = DateTime.Today.AddDays(7)
        };

        var time = new TimePicker
        {
            Time = new TimeSpan(18, 0, 0)
        };

        stack.Children.Add(
            Ui.Heading("Create Remote Event"));

        Ui.Field(stack, "Event Name", name);
        Ui.Field(stack, "Address", address);
        Ui.Field(stack, "Description", description);
        Ui.Field(stack, "Capacity", capacity);
        Ui.Field(stack, "Date", date);
        Ui.Field(stack, "Time", time);

        stack.Children.Add(
            Ui.Button("Save to Web API", async () =>
            {
                if (Ui.Empty(
                    name, address, description, capacity) ||
                    !int.TryParse(
                        capacity.Text, out int max) ||
                    max < 1)
                {
                    await DisplayAlert(
                        "Invalid",
                        "Complete the form with a valid capacity.",
                        "OK");
                    return;
                }

                var eventDate =
                    date.Date.Date.Add(time.Time);

                if (eventDate <= DateTime.Now)
                {
                    await DisplayAlert(
                        "Invalid",
                        "Select a future event.",
                        "OK");
                    return;
                }

                try
                {
                    await RemoteEventService.CreateEvent(
                        new RemoteEvent
                        {
                            Name = name.Text!.Trim(),
                            Address = address.Text!.Trim(),
                            Description =
                                description.Text!.Trim(),
                            Capacity = max,
                            Date = eventDate
                        });

                    await DisplayAlert(
                        "Saved",
                        "Event saved on the remote server.",
                        "OK");

                    await Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert(
                        "API Error", ex.Message, "OK");
                }
            }, true));

        Content = Ui.View(stack);
    }
}

// Displays event details and RSVP responses.
public class RemoteEventDetailsPage : ContentPage
{
    private readonly RemoteEvent ev;

    private readonly VerticalStackLayout stack =
        Ui.Stack();

    public RemoteEventDetailsPage(RemoteEvent item)
    {
        ev = item;
        Title = "Remote Event Details";
        Content = Ui.View(stack);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Refresh();
    }

    private async Task Refresh()
    {
        stack.Children.Clear();

        stack.Children.Add(
            Ui.Heading(ev.Name));

        stack.Children.Add(
            Ui.Text(ev.Description));

        stack.Children.Add(
            Ui.Text("Location: " + ev.Address));

        stack.Children.Add(
            Ui.Text("Date: " +
                ev.Date.ToString(
                    "MMMM d, yyyy h:mm tt")));

        try
        {
            var rsvps =
                await RemoteEventService.GetRsvps(ev.Id);

            stack.Children.Add(
                Ui.Text(
                    $"Accepted: {rsvps.Count} / {ev.Capacity}"));

            foreach (var person in rsvps)
            {
                stack.Children.Add(
                    Ui.Text("✓ " + person.Name));
            }

            if (ev.Date > DateTime.Now)
            {
                stack.Children.Add(
                    Ui.Button("Submit RSVP", async () =>
                    {
                        string? name =
                            await DisplayPromptAsync(
                                "RSVP",
                                "Enter your name:");

                        if (string.IsNullOrWhiteSpace(name))
                            return;

                        try
                        {
                            await RemoteEventService.SaveRsvp(
                                ev.Id, name);

                            await DisplayAlert(
                                "Success",
                                "RSVP saved to remote database.",
                                "OK");

                            await Refresh();
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert(
                                "Error", ex.Message, "OK");
                        }
                    }, true));
            }
        }
        catch (Exception ex)
        {
            stack.Children.Add(
                Ui.Text("API Error: " + ex.Message));
        }
    }
}
