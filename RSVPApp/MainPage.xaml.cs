
using SQLite;
using System.Security.Cryptography;
using System.Text;

namespace RSVPApp;

// SQLITE DATABASE TABLES

[Table("Users")]
public class UserRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique, NotNull]
    public string Email { get; set; } = "";

    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

[Table("Events")]
public class EventRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int HostUserId { get; set; }

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime Deadline { get; set; }
    public int Capacity { get; set; }
}

[Table("RSVPs")]
public class RsvpRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EventId { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Status { get; set; } = "Accepted";
}

// DATABASE ACCESS

public static class Store
{
    private static SQLiteAsyncConnection? connection;
    private static readonly SemaphoreSlim gate = new(1, 1);

    public static UserRecord? CurrentUser { get; set; }
    public static bool Guest => CurrentUser == null;

    private static async Task<SQLiteAsyncConnection> Db()
    {
        if (connection != null)
            return connection;

        await gate.WaitAsync();

        try
        {
            if (connection == null)
            {
                var path = Path.Combine(
                    FileSystem.AppDataDirectory, "rsvp.db3");

                var db = new SQLiteAsyncConnection(path);

                await db.CreateTableAsync<UserRecord>();
                await db.CreateTableAsync<EventRecord>();
                await db.CreateTableAsync<RsvpRecord>();

                connection = db;
            }
        }
        finally
        {
            gate.Release();
        }

        return connection!;
    }

    private static string Hash(
        string password, byte[]? salt = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(16);

        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, 120000,
            HashAlgorithmName.SHA256, 32);

        return Convert.ToBase64String(salt) + ":" +
               Convert.ToBase64String(derived);
    }

    public static async Task<string?> Register(
        string name, string email,
        string password, string phone)
    {
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(phone) ||
            !email.Contains('@') ||
            password.Length < 8)
        {
            return "Complete all fields with a valid email and password of at least 8 characters.";
        }

        var db = await Db();

        var existing = await db.Table<UserRecord>()
            .Where(x => x.Email == email)
            .FirstOrDefaultAsync();

        if (existing != null)
            return "That email is already registered.";

        try
        {
            await db.InsertAsync(new UserRecord
            {
                Name = name.Trim(),
                Email = email,
                Phone = phone.Trim(),
                PasswordHash = Hash(password)
            });
        }
        catch (SQLiteException)
        {
            return "Unable to save account.";
        }

        return null;
    }

    public static async Task<bool> Login(
        string email, string password)
    {
        var db = await Db();

        var user = await db.Table<UserRecord>()
            .Where(x =>
                x.Email == email.Trim().ToLowerInvariant())
            .FirstOrDefaultAsync();

        if (user == null)
            return false;

        var pieces = user.PasswordHash.Split(':');

        if (pieces.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(pieces[0]);
            var actual = Convert.FromBase64String(pieces[1]);

            var test = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, 120000,
                HashAlgorithmName.SHA256, 32);

            if (!CryptographicOperations.FixedTimeEquals(
                actual, test))
                return false;
        }
        catch (FormatException)
        {
            return false;
        }

        CurrentUser = user;
        return true;
    }

    public static async Task<int> AddEvent(
        EventRecord item)
    {
        var db = await Db();

        await db.InsertAsync(item);

        return item.Id;
    }

    public static async Task<List<EventRecord>> Events()
    {
        return await (await Db())
            .Table<EventRecord>()
            .ToListAsync();
    }

    public static async Task<List<RsvpRecord>> Rsvps(
        int eventId)
    {
        return await (await Db())
            .Table<RsvpRecord>()
            .Where(x => x.EventId == eventId)
            .ToListAsync();
    }

    public static async Task<bool> Attending(
        int eventId, int userId)
    {
        return await (await Db())
            .Table<RsvpRecord>()
            .Where(x =>
                x.EventId == eventId &&
                x.UserId == userId &&
                x.Status == "Accepted")
            .CountAsync() > 0;
    }

    public static async Task<string?> SaveRsvp(
        EventRecord item,
        string name, string email, string phone)
    {
        if (item.Date <= DateTime.Now ||
            item.Deadline.Date < DateTime.Today)
            return "RSVP is closed.";

        var db = await Db();
        var rsvps = await Rsvps(item.Id);

        var normalizedEmail =
            email.Trim().ToLowerInvariant();

        int userId = CurrentUser?.Id ?? 0;

        var existing = rsvps.FirstOrDefault(x =>
            userId > 0
                ? x.UserId == userId
                : x.Email == normalizedEmail);

        if (existing == null &&
            rsvps.Count(x =>
                x.Status == "Accepted") >= item.Capacity)
        {
            return "Event is full.";
        }

        var record = existing ?? new RsvpRecord
        {
            EventId = item.Id,
            UserId = userId
        };

        record.Name = name.Trim();
        record.Email = normalizedEmail;
        record.Phone = phone.Trim();
        record.Status = "Accepted";

        if (existing == null)
            await db.InsertAsync(record);
        else
            await db.UpdateAsync(record);

        return null;
    }

    public static async Task<string> HostName(int id)
    {
        var user = await (await Db())
            .Table<UserRecord>()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync();

        return user?.Name ?? "Unknown";
    }
}

// UI HELPERS

public static class Ui
{
    public static VerticalStackLayout Stack() =>
        new()
        {
            Padding = 20,
            Spacing = 12
        };

    public static Label Heading(string text) =>
        new()
        {
            Text = text,
            FontSize = 23,
            FontAttributes = FontAttributes.Bold
        };

    public static Label Text(string text) =>
        new() { Text = text };

    public static Entry Entry(
        string hint, bool secret = false) =>
        new()
        {
            Placeholder = hint,
            IsPassword = secret
        };

    public static Button Button(
        string text,
        Func<Task> action,
        bool green = false)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb(
                green ? "#218653" : "#153454")
        };

        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;

            try
            {
                await action();
            }
            finally
            {
                button.IsEnabled = true;
            }
        };

        return button;
    }

    public static ScrollView View(
        VerticalStackLayout stack) =>
        new() { Content = stack };

    public static void Field(
        VerticalStackLayout stack,
        string label,
        View field)
    {
        stack.Children.Add(new Label
        {
            Text = label,
            FontAttributes = FontAttributes.Bold
        });

        stack.Children.Add(field);
    }

    public static bool Empty(params Entry[] entries) =>
        entries.Any(x =>
            string.IsNullOrWhiteSpace(x.Text));

    public static Label Placeholder() =>
        new()
        {
            Text = "EVENT",
            HeightRequest = 90,
            BackgroundColor =
                Color.FromArgb("#DDE9F4"),
            HorizontalTextAlignment =
                TextAlignment.Center,
            VerticalTextAlignment =
                TextAlignment.Center,
            FontAttributes =
                FontAttributes.Bold
        };
}

// LOGIN PAGE

public partial class MainPage : ContentPage
{
    private readonly Entry email =
        Ui.Entry("Email");

    private readonly Entry password =
        Ui.Entry("Password", true);

    public MainPage()
    {
        InitializeComponent();

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Welcome to RSVP"));

        stack.Children.Add(
            Ui.Text("Find events and manage your invitations."));

        Ui.Field(stack, "Email", email);
        Ui.Field(stack, "Password", password);

        stack.Children.Add(Ui.Button("Login", async () =>
        {
            if (Ui.Empty(email, password))
            {
                await DisplayAlert(
                    "Missing Information",
                    "Enter your email and password.",
                    "OK");
                return;
            }

            bool valid = await Store.Login(
                email.Text!, password.Text!);

            if (!valid)
            {
                await DisplayAlert(
                    "Login Failed",
                    "Incorrect email or password.",
                    "OK");
                return;
            }

            password.Text = "";

            await Navigation.PushAsync(new HomePage());
        }));

        stack.Children.Add(
            Ui.Button("Continue as Guest", async () =>
            {
                Store.CurrentUser = null;

                await Navigation.PushAsync(
                    new HomePage());
            }));

        stack.Children.Add(
            Ui.Button("Create Account", async () =>
                await Navigation.PushAsync(
                    new AddUserPage())));

        Content = Ui.View(stack);
    }
}

// ACCOUNT REGISTRATION

public class AddUserPage : ContentPage
{
    public AddUserPage()
    {
        Title = "Create Account";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Create Account"));

        var name = Ui.Entry("Full name");
        var email = Ui.Entry("Email");
        var pass = Ui.Entry("Password", true);
        var phone = Ui.Entry("Mobile phone");

        Ui.Field(stack, "Name", name);
        Ui.Field(stack, "Email", email);
        Ui.Field(stack, "Password (8+ characters)", pass);
        Ui.Field(stack, "Mobile Phone", phone);

        stack.Children.Add(
            Ui.Button("Create Account", async () =>
            {
                if (Ui.Empty(name, email, pass, phone))
                {
                    await DisplayAlert(
                        "Missing Information",
                        "Complete every field.",
                        "OK");
                    return;
                }

                var error = await Store.Register(
                    name.Text!,
                    email.Text!,
                    pass.Text!,
                    phone.Text!);

                if (error != null)
                {
                    await DisplayAlert(
                        "Cannot Register", error, "OK");
                    return;
                }

                await DisplayAlert(
                    "Saved",
                    "Account saved to SQLite. Please log in.",
                    "OK");

                await Navigation.PopAsync();
            }, true));

        stack.Children.Add(
            Ui.Button("Cancel", async () =>
                await Navigation.PopAsync()));

        Content = Ui.View(stack);
    }
}

// HOME TABS

public class HomePage : TabbedPage
{
    public HomePage()
    {
        Title = Store.Guest
            ? "RSVP — Guest"
            : "RSVP";

        BarBackgroundColor =
            Color.FromArgb("#153454");

        SelectedTabColor = Colors.White;

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Logout",
            Command = new Command(async () =>
            {
                Store.CurrentUser = null;

                await Navigation.PopToRootAsync();
            })
        });

        Children.Add(new EventsPage());
        Children.Add(new InvitationsPage());
        Children.Add(new EventsPage(true));
        Children.Add(new RemoteEventsPage());
    }
}

// EVENTS LIST

public class EventsPage : ContentPage
{
    private readonly bool past;
    private string filter;

    private readonly VerticalStackLayout list =
        new() { Spacing = 14 };

    public EventsPage(bool past = false)
    {
        this.past = past;

        filter = Store.Guest
            ? "All"
            : "Attending";

        Title = past ? "Past Events" : "Events";

        var stack = Ui.Stack();

        stack.Children.Add(Ui.Heading(Title));

        if (!past)
        {
            var buttons = new HorizontalStackLayout
            {
                Spacing = 5
            };

            foreach (var selected in Store.Guest
                ? new[] { "All" }
                : new[] { "All", "Attending", "Hosting" })
            {
                buttons.Children.Add(
                    Ui.Button(selected, async () =>
                    {
                        filter = selected;
                        await Refresh();
                    }));
            }

            stack.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = buttons
            });

            if (!Store.Guest)
            {
                stack.Children.Add(
                    Ui.Button("Create Event", async () =>
                        await Navigation.PushAsync(
                            new AddEventPage()), true));
            }
        }

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

        list.Children.Add(
            Ui.Text(past
                ? "Previous gatherings"
                : "Showing: " + filter));

        var all = await Store.Events();

        int currentId = Store.CurrentUser?.Id ?? 0;

        var results = new List<EventRecord>();

        foreach (var ev in all)
        {
            if (past != (ev.Date < DateTime.Now))
                continue;

            bool hosting =
                currentId > 0 &&
                ev.HostUserId == currentId;

            bool attending =
                currentId > 0 &&
                await Store.Attending(ev.Id, currentId);

            if (past &&
                (Store.Guest || (!hosting && !attending)))
                continue;

            if (!past &&
                filter == "Attending" && !attending)
                continue;

            if (!past &&
                filter == "Hosting" && !hosting)
                continue;

            results.Add(ev);
        }

        if (results.Count == 0)
        {
            list.Children.Add(Ui.Text(
                "No events in this list. Try All or create an event."));
        }

        foreach (var ev in results.OrderBy(x => x.Date))
        {
            var card = new VerticalStackLayout
            {
                Spacing = 8,
                Padding = 12,
                BackgroundColor = Colors.White
            };

            card.Children.Add(Ui.Placeholder());
            card.Children.Add(Ui.Heading(ev.Name));

            card.Children.Add(Ui.Text(
                ev.Date.ToString(
                    "MMMM d, yyyy • h:mm tt")));

            card.Children.Add(Ui.Text(ev.Address));

            card.Children.Add(
                Ui.Button("View Details", async () =>
                    await Navigation.PushAsync(
                        new EventDetailsPage(ev))));

            list.Children.Add(card);
        }
    }
}

// CREATE EVENT

public class AddEventPage : ContentPage
{
    public AddEventPage()
    {
        Title = "Add Event";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Create Event"));

        var name = Ui.Entry("Event name");
        var address = Ui.Entry("Address");
        var description = Ui.Entry("Description");
        var capacity = Ui.Entry("Maximum attendees");

        capacity.Keyboard = Keyboard.Numeric;

        var date = new DatePicker
        {
            Date = DateTime.Today.AddDays(14)
        };

        var time = new TimePicker
        {
            Time = new TimeSpan(18, 0, 0)
        };

        var deadline = new DatePicker
        {
            Date = DateTime.Today.AddDays(12)
        };

        stack.Children.Add(Ui.Text(
            "Host: " +
            (Store.CurrentUser?.Name ?? "Not logged in")));

        Ui.Field(stack, "Event Name", name);
        Ui.Field(stack, "Address", address);
        Ui.Field(stack, "Description", description);
        Ui.Field(stack, "Capacity", capacity);
        Ui.Field(stack, "Event Date", date);
        Ui.Field(stack, "Event Time", time);
        Ui.Field(stack, "RSVP Deadline", deadline);

        stack.Children.Add(
            Ui.Button("Save Event", async () =>
            {
                if (Store.CurrentUser == null)
                {
                    await DisplayAlert(
                        "Login Required",
                        "Log in to host an event.",
                        "OK");
                    return;
                }

                if (Ui.Empty(
                        name, address, description, capacity) ||
                    !int.TryParse(
                        capacity.Text, out int maximum) ||
                    maximum < 1)
                {
                    await DisplayAlert(
                        "Invalid",
                        "Complete all fields and enter a positive capacity.",
                        "OK");
                    return;
                }

                // CORRECTED MAUI DATE/TIME CODE
                // No HasValue or Value required.

                DateTime eventDate =
                    date.Date.Date.Add(time.Time);

                DateTime limit =
                    deadline.Date.Date;

                if (eventDate <= DateTime.Now ||
                    limit < DateTime.Today ||
                    limit > eventDate.Date)
                {
                    await DisplayAlert(
                        "Invalid Date",
                        "Select a future event and valid RSVP deadline.",
                        "OK");
                    return;
                }

                await Store.AddEvent(new EventRecord
                {
                    HostUserId = Store.CurrentUser.Id,
                    Name = name.Text!.Trim(),
                    Address = address.Text!.Trim(),
                    Description = description.Text!.Trim(),
                    Capacity = maximum,
                    Date = eventDate,
                    Deadline = limit
                });

                await DisplayAlert(
                    "Saved",
                    "Event saved in SQLite.",
                    "OK");

                await Navigation.PopAsync();
            }, true));

        stack.Children.Add(
            Ui.Button("Cancel", async () =>
                await Navigation.PopAsync()));

        Content = Ui.View(stack);
    }
}

// EVENT DETAILS

public class EventDetailsPage : ContentPage
{
    public EventDetailsPage(EventRecord ev)
    {
        Title = "Event Details";

        var stack = Ui.Stack();

        Content = Ui.View(stack);

        _ = Load();

        async Task Load()
        {
            stack.Children.Clear();

            stack.Children.Add(Ui.Placeholder());
            stack.Children.Add(Ui.Heading(ev.Name));
            stack.Children.Add(Ui.Text(ev.Description));

            stack.Children.Add(Ui.Text(
                "Host: " +
                await Store.HostName(ev.HostUserId)));

            stack.Children.Add(Ui.Text(
                "Address: " + ev.Address));

            stack.Children.Add(Ui.Text(
                "Date: " +
                ev.Date.ToString(
                    "MMMM d, yyyy • h:mm tt")));

            stack.Children.Add(Ui.Text(
                "RSVP deadline: " +
                ev.Deadline.ToString("MMMM d, yyyy")));

            var accepted = (await Store.Rsvps(ev.Id))
                .Where(x => x.Status == "Accepted")
                .ToList();

            stack.Children.Add(Ui.Text(
                $"Attendees: {accepted.Count} / {ev.Capacity}"));

            foreach (var person in accepted)
            {
                stack.Children.Add(Ui.Text(
                    "✓ " + person.Name + " — Accepted"));
            }

            if (ev.Date <= DateTime.Now)
            {
                stack.Children.Add(
                    Ui.Text("Event ended"));
            }
            else if (ev.Deadline.Date < DateTime.Today)
            {
                stack.Children.Add(
                    Ui.Text("RSVP closed"));
            }
            else if (
                accepted.Count >= ev.Capacity &&
                !accepted.Any(x =>
                    x.UserId > 0 &&
                    x.UserId == Store.CurrentUser?.Id))
            {
                stack.Children.Add(
                    Ui.Text("Event full"));
            }
            else
            {
                stack.Children.Add(
                    Ui.Button("RSVP", async () =>
                        await Navigation.PushAsync(
                            new RsvpPage(ev)), true));
            }

            stack.Children.Add(
                Ui.Button("Back", async () =>
                    await Navigation.PopAsync()));
        }
    }
}

// RSVP PAGE

public class RsvpPage : ContentPage
{
    public RsvpPage(EventRecord ev)
    {
        Title = "RSVP";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("RSVP for " + ev.Name));

        var name = Ui.Entry("Name");
        var email = Ui.Entry("Email");
        var phone = Ui.Entry("Phone");

        if (Store.CurrentUser is { } user)
        {
            name.Text = user.Name;
            email.Text = user.Email;
            phone.Text = user.Phone;

            name.IsReadOnly = true;
            email.IsReadOnly = true;
            phone.IsReadOnly = true;
        }

        Ui.Field(stack, "Name", name);
        Ui.Field(stack, "Email", email);
        Ui.Field(stack, "Mobile Phone", phone);

        stack.Children.Add(
            Ui.Button("Save RSVP", async () =>
            {
                if (Ui.Empty(name, email, phone) ||
                    !email.Text!.Contains('@'))
                {
                    await DisplayAlert(
                        "Invalid",
                        "Enter a name, email, and phone.",
                        "OK");
                    return;
                }

                var error = await Store.SaveRsvp(
                    ev,
                    name.Text!,
                    email.Text!,
                    phone.Text!);

                if (error != null)
                {
                    await DisplayAlert(
                        "Cannot RSVP", error, "OK");
                    return;
                }

                await DisplayAlert(
                    "Saved",
                    "Your RSVP was saved to SQLite.",
                    "OK");

                await Navigation.PopAsync();
            }, true));

        stack.Children.Add(
            Ui.Button("Cancel", async () =>
                await Navigation.PopAsync()));

        Content = Ui.View(stack);
    }
}

// INVITATION MANAGEMENT

public class InvitationsPage : ContentPage
{
    private readonly VerticalStackLayout list =
        new() { Spacing = 12 };

    public InvitationsPage()
    {
        Title = "RSVPs";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Manage Invitations"));

        stack.Children.Add(list);

        Content = Ui.View(stack);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        list.Children.Clear();

        if (Store.Guest)
        {
            list.Children.Add(Ui.Text(
                "Log in to view RSVPs for events you host."));
            return;
        }

        var all = await Store.Events();

        var hosted = all.Where(x =>
            x.HostUserId == Store.CurrentUser!.Id &&
            x.Date >= DateTime.Now)
            .ToList();

        if (hosted.Count == 0)
        {
            list.Children.Add(
                Ui.Text("No upcoming hosted events."));
        }

        foreach (var ev in hosted)
        {
            list.Children.Add(
                Ui.Button(ev.Name, async () =>
                    await Navigation.PushAsync(
                        new InvitationDetailsPage(ev))));
        }
    }
}

// INVITATION DETAILS

public class InvitationDetailsPage : ContentPage
{
    public InvitationDetailsPage(EventRecord ev)
    {
        Title = "Invitations";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading(ev.Name));

        Content = Ui.View(stack);

        _ = Load();

        async Task Load()
        {
            var rsvps = await Store.Rsvps(ev.Id);

            stack.Children.Add(
                Ui.Heading("Pending"));

            foreach (var record in rsvps.Where(
                x => x.Status == "Pending"))
            {
                stack.Children.Add(Ui.Text(
                    "… " + record.Name + " — Pending"));
            }

            if (!rsvps.Any(
                x => x.Status == "Pending"))
            {
                stack.Children.Add(
                    Ui.Text("No pending invitations."));
            }

            stack.Children.Add(
                Ui.Heading("Accepted"));

            foreach (var record in rsvps.Where(
                x => x.Status == "Accepted"))
            {
                stack.Children.Add(Ui.Text(
                    "✓ " + record.Name + " — Accepted"));
            }

            if (!rsvps.Any(
                x => x.Status == "Accepted"))
            {
                stack.Children.Add(
                    Ui.Text("No accepted RSVPs yet."));
            }

            stack.Children.Add(
                Ui.Button("Event Details", async () =>
                    await Navigation.PushAsync(
                        new EventDetailsPage(ev))));
        }
    }
}
