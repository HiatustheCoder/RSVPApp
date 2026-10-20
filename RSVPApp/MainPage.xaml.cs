using System;
using System.Collections.Generic;
using System.Linq;

namespace RSVPApp;

// Hard-coded user information for Week 2.
public static class Session
{
    public static bool IsGuest { get; set; } = true;

    public const string Name = "Dalvin Wolff";
    public const string Email = "dalvin@example.com";
    public const string Password = "Welcome123!";
    public const string Phone = "555-0100";
}

// Event information displayed by the application.
public class EventInfo
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";

    public DateTime Date { get; set; }
    public DateTime Deadline { get; set; }

    public int Capacity { get; set; }
    public bool Attending { get; set; }
    public bool Hosting { get; set; }

    public List<string> Accepted { get; set; } = new();
    public List<string> Pending { get; set; } = new();

    public string ImageName { get; set; } = "dotnet_bot.png";
}

// Shared sample events.
public static class SampleData
{
    public static List<EventInfo> Events { get; } = new()
    {
        new EventInfo
        {
            Name = "Community Game Night",
            Host = Session.Name,
            Address = "100 Main Street, Richmond, VA",
            Description =
                "Join us for board games, snacks, and a relaxed evening. " +
                "Bring your favorite game if you would like.",
            Date = DateTime.Today.AddDays(14).AddHours(18),
            Deadline = DateTime.Today.AddDays(12).AddHours(23),
            Capacity = 20,
            Attending = true,
            Hosting = true,
            Accepted = new List<string>
            {
                Session.Name,
                "Jordan Smith",
                "Alex Johnson"
            },
            Pending = new List<string>
            {
                "Taylor Brown",
                "Morgan Lee"
            }
        },

        new EventInfo
        {
            Name = "Weekend Picnic",
            Host = "Jordan Smith",
            Address = "250 Park Avenue, Richmond, VA",
            Description =
                "An afternoon picnic at the park. Bring a chair and water.",
            Date = DateTime.Today.AddDays(21).AddHours(12),
            Deadline = DateTime.Today.AddDays(19).AddHours(23),
            Capacity = 30,
            Attending = false,
            Hosting = false,
            Accepted = new List<string>
            {
                "Jordan Smith"
            }
        },

        new EventInfo
        {
            Name = "Study Meetup",
            Host = "Alex Johnson",
            Address = "75 Library Lane, Richmond, VA",
            Description =
                "A completed study meetup for reviewing programming concepts.",
            Date = DateTime.Today.AddDays(-7).AddHours(15),
            Deadline = DateTime.Today.AddDays(-9),
            Capacity = 10,
            Attending = true,
            Hosting = false,
            Accepted = new List<string>
            {
                Session.Name,
                "Alex Johnson"
            }
        }
    };
}

// Shared helpers for building the pages.
public static class Ui
{
    public static Label Heading(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        };
    }

    public static Button Action(
        string text,
        Action clicked,
        bool green = false)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb(
                green ? "#218653" : "#153454")
        };

        button.Clicked += (_, _) => clicked();

        return button;
    }

    public static VerticalStackLayout Stack()
    {
        return new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 14
        };
    }

    public static View Form(VerticalStackLayout stack)
    {
        return new ScrollView
        {
            Content = stack
        };
    }

    public static bool Missing(params Entry[] fields)
    {
        return fields.Any(
            field => string.IsNullOrWhiteSpace(field.Text));
    }

    public static Label FieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold
        };
    }

    public static void AddField(
        VerticalStackLayout stack,
        string label,
        View field)
    {
        stack.Children.Add(FieldLabel(label));
        stack.Children.Add(field);
    }

    // Corrected image method.
    public static Image EventImage(EventInfo item)
    {
        var image = new Image
        {
            Source = item.ImageName,
            HeightRequest = 150,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#E4EDF5")
        };

        SemanticProperties.SetDescription(
            image,
            "Placeholder event image");

        return image;
    }
}

// LOGIN PAGE
public partial class MainPage : ContentPage
{
    private readonly Entry email = new()
    {
        Placeholder = "Email",
        Keyboard = Keyboard.Email
    };

    private readonly Entry password = new()
    {
        Placeholder = "Password",
        IsPassword = true
    };

    public MainPage()
    {
        InitializeComponent();

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Welcome to RSVP"));

        stack.Children.Add(new Label
        {
            Text = "Find events and manage your invitations."
        });

        Ui.AddField(stack, "Email", email);
        Ui.AddField(stack, "Password", password);

        stack.Children.Add(
            Ui.Action("Login", Login));

        stack.Children.Add(
            Ui.Action("Continue as Guest", Guest));

        stack.Children.Add(
            Ui.Action("Create Account", async () =>
            {
                await Navigation.PushAsync(new AddUserPage());
            }));

        stack.Children.Add(new Label
        {
            Text =
                "Demo login:\n" +
                "dalvin@example.com\n" +
                "Welcome123!",
            FontSize = 14,
            TextColor = Colors.Gray
        });

        Content = Ui.Form(stack);
    }

    private async void Login()
    {
        if (Ui.Missing(email, password))
        {
            await DisplayAlert(
                "Missing Information",
                "Enter your email and password.",
                "OK");

            return;
        }

        bool valid =
            string.Equals(
                email.Text.Trim(),
                Session.Email,
                StringComparison.OrdinalIgnoreCase)
            && password.Text == Session.Password;

        if (!valid)
        {
            await DisplayAlert(
                "Login Failed",
                "The email or password is incorrect.",
                "OK");

            return;
        }

        Session.IsGuest = false;
        password.Text = "";

        await Navigation.PushAsync(new HomePage());
    }

    private async void Guest()
    {
        Session.IsGuest = true;
        password.Text = "";

        await Navigation.PushAsync(new HomePage());
    }
}

// ADD USER PAGE
public class AddUserPage : ContentPage
{
    private readonly Entry name = new()
    {
        Placeholder = "Full name"
    };

    private readonly Entry email = new()
    {
        Placeholder = "Email",
        Keyboard = Keyboard.Email
    };

    private readonly Entry password = new()
    {
        Placeholder = "Password",
        IsPassword = true
    };

    private readonly Entry phone = new()
    {
        Placeholder = "Mobile phone",
        Keyboard = Keyboard.Telephone
    };

    public AddUserPage()
    {
        Title = "Create Account";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Create Account"));

        Ui.AddField(stack, "Name", name);
        Ui.AddField(stack, "Email", email);
        Ui.AddField(stack, "Password", password);
        Ui.AddField(stack, "Mobile Phone", phone);

        stack.Children.Add(
            Ui.Action("Add / OK", Validate, true));

        stack.Children.Add(
            Ui.Action("Cancel", async () =>
            {
                await Navigation.PopAsync();
            }));

        Content = Ui.Form(stack);
    }

    private async void Validate()
    {
        if (Ui.Missing(name, email, password, phone))
        {
            await DisplayAlert(
                "Missing Information",
                "Complete all account fields.",
                "OK");

            return;
        }

        await DisplayAlert(
            "Validation Successful",
            "All fields are complete. Account saving will be added later. " +
            "Use the demo credentials to log in.",
            "OK");

        await Navigation.PopAsync();
    }
}

// MAIN TABS
public class HomePage : TabbedPage
{
    public HomePage()
    {
        Title = Session.IsGuest
            ? "RSVP — Guest"
            : "RSVP";

        BarBackgroundColor = Color.FromArgb("#153454");
        SelectedTabColor = Colors.White;
        UnselectedTabColor = Color.FromArgb("#B8CBDC");

        NavigationPage.SetHasBackButton(this, false);

        ToolbarItems.Add(new ToolbarItem
        {
            Text = Session.IsGuest
                ? "Exit Guest"
                : "Logout",

            Command = new Command(async () =>
            {
                Session.IsGuest = true;

                await Navigation.PopToRootAsync();
            })
        });

        Children.Add(new EventsPage());
        Children.Add(new InvitationsPage());
        Children.Add(new EventsPage(past: true));
    }

    protected override bool OnBackButtonPressed()
    {
        Session.IsGuest = true;

        return base.OnBackButtonPressed();
    }
}

// EVENTS AND PAST EVENTS
public class EventsPage : ContentPage
{
    private readonly bool past;
    private string filter;

    private readonly VerticalStackLayout list = new()
    {
        Spacing = 16
    };

    public EventsPage(bool past = false)
    {
        this.past = past;

        filter = Session.IsGuest
            ? "All"
            : "Attending";

        Title = past
            ? "Past Events"
            : "Events";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading(Title));

        if (!past)
        {
            var filters = new HorizontalStackLayout
            {
                Spacing = 8
            };

            filters.Children.Add(
                Ui.Action("All", () =>
                {
                    SetFilter("All");
                }));

            if (!Session.IsGuest)
            {
                filters.Children.Add(
                    Ui.Action("Attending", () =>
                    {
                        SetFilter("Attending");
                    }));

                filters.Children.Add(
                    Ui.Action("Hosting", () =>
                    {
                        SetFilter("Hosting");
                    }));
            }

            stack.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = filters
            });

            if (!Session.IsGuest)
            {
                stack.Children.Add(
                    Ui.Action("Create Event", async () =>
                    {
                        await Navigation.PushAsync(
                            new AddEventPage());
                    }, true));
            }
        }

        stack.Children.Add(list);

        Content = Ui.Form(stack);

        Refresh();
    }

    private void SetFilter(string selected)
    {
        filter = selected;

        Refresh();
    }

    private void Refresh()
    {
        list.Children.Clear();

        IEnumerable<EventInfo> events = SampleData.Events;

        events = past
            ? events.Where(item => item.Date < DateTime.Now)
            : events.Where(item => item.Date >= DateTime.Now);

        if (past)
        {
            events = Session.IsGuest
                ? Enumerable.Empty<EventInfo>()
                : events.Where(
                    item => item.Attending || item.Hosting);
        }
        else if (filter == "Attending")
        {
            events = events.Where(item => item.Attending);
        }
        else if (filter == "Hosting")
        {
            events = events.Where(item => item.Hosting);
        }

        list.Children.Add(new Label
        {
            Text = past
                ? "Previous gatherings"
                : $"Showing: {filter}",

            FontAttributes = FontAttributes.Bold
        });

        var results = events
            .OrderBy(item => item.Date)
            .ToList();

        if (results.Count == 0)
        {
            list.Children.Add(new Label
            {
                Text = Session.IsGuest && past
                    ? "Log in to view events you hosted or attended."
                    : "No events in this list."
            });
        }

        foreach (var item in results)
        {
            var card = new VerticalStackLayout
            {
                Spacing = 10,
                Padding = 16,
                BackgroundColor = Colors.White
            };

            card.Children.Add(
                Ui.EventImage(item));

            card.Children.Add(
                Ui.Heading(item.Name));

            card.Children.Add(new Label
            {
                Text = item.Date.ToString(
                    "MMMM d, yyyy • h:mm tt")
            });

            card.Children.Add(new Label
            {
                Text = item.Address
            });

            card.Children.Add(
                Ui.Action("View Details", async () =>
                {
                    await Navigation.PushAsync(
                        new EventDetailsPage(item));
                }));

            list.Children.Add(card);
        }
    }
}

// EVENT DETAILS
public class EventDetailsPage : ContentPage
{
    public EventDetailsPage(EventInfo item)
    {
        Title = "Event Details";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.EventImage(item));

        stack.Children.Add(
            Ui.Heading(item.Name));

        stack.Children.Add(new Label
        {
            Text = item.Description
        });

        stack.Children.Add(new Label
        {
            Text = $"Host: {item.Host}"
        });

        stack.Children.Add(new Label
        {
            Text = $"Address: {item.Address}"
        });

        stack.Children.Add(new Label
        {
            Text = $"Date: {item.Date:MMMM d, yyyy}"
        });

        stack.Children.Add(new Label
        {
            Text = $"Time: {item.Date:h:mm tt}"
        });

        stack.Children.Add(new Label
        {
            Text =
                $"RSVP deadline: {item.Deadline:MMMM d, yyyy • h:mm tt}"
        });

        stack.Children.Add(new Label
        {
            Text =
                $"Attendees: {item.Accepted.Count} / {item.Capacity}"
        });

        stack.Children.Add(
            Ui.FieldLabel("Attending"));

        foreach (string person in item.Accepted)
        {
            stack.Children.Add(new Label
            {
                Text = $"✓ {person} — Accepted",
                TextColor = Color.FromArgb("#218653")
            });
        }

        bool ended = item.Date <= DateTime.Now;
        bool closed = item.Deadline < DateTime.Now;
        bool full = item.Accepted.Count >= item.Capacity;

        if (ended || closed || full)
        {
            stack.Children.Add(new Label
            {
                Text = ended
                    ? "Event Ended"
                    : full
                        ? "Event Full"
                        : "RSVP Closed",

                FontAttributes = FontAttributes.Bold
            });
        }
        else
        {
            stack.Children.Add(
                Ui.Action("RSVP", async () =>
                {
                    await Navigation.PushAsync(
                        new RsvpPage(item));
                }, true));
        }

        stack.Children.Add(
            Ui.Action("Back", async () =>
            {
                await Navigation.PopAsync();
            }));

        Content = Ui.Form(stack);
    }
}

// ADD EVENT PAGE
public class AddEventPage : ContentPage
{
    private readonly Entry host = new()
    {
        Placeholder = "Host name",
        Text = Session.Name
    };

    private readonly Entry name = new()
    {
        Placeholder = "Event name"
    };

    private readonly Entry address = new()
    {
        Placeholder = "Event address"
    };

    private readonly Entry capacity = new()
    {
        Placeholder = "Maximum attendees",
        Keyboard = Keyboard.Numeric
    };

    private readonly DatePicker date = new()
    {
        Date = DateTime.Today.AddDays(14)
    };

    private readonly TimePicker time = new()
    {
        Time = new TimeSpan(18, 0, 0)
    };

    private readonly DatePicker deadline = new()
    {
        Date = DateTime.Today.AddDays(12)
    };

    public AddEventPage()
    {
        Title = "Add Event";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Create Event"));

        Ui.AddField(stack, "Host Name", host);
        Ui.AddField(stack, "Event Name", name);
        Ui.AddField(stack, "Address", address);
        Ui.AddField(stack, "Maximum Attendees", capacity);
        Ui.AddField(stack, "Event Date", date);
        Ui.AddField(stack, "Event Time", time);
        Ui.AddField(stack, "RSVP Deadline", deadline);

        stack.Children.Add(
            Ui.Action("Save", Validate, true));

        stack.Children.Add(
            Ui.Action("Cancel", async () =>
            {
                await Navigation.PopAsync();
            }));

        Content = Ui.Form(stack);
    }

    private async void Validate()
    {
        if (Ui.Missing(host, name, address, capacity))
        {
            await DisplayAlert(
                "Missing Information",
                "Complete all event fields.",
                "OK");

            return;
        }

        if (!int.TryParse(capacity.Text, out int maximum)
            || maximum < 1)
        {
            await DisplayAlert(
                "Invalid Capacity",
                "Maximum attendees must be a whole number greater than zero.",
                "OK");

            return;
        }

        DateTime? selectedDate = date.Date;
        TimeSpan? selectedTime = time.Time;
        DateTime? selectedDeadline = deadline.Date;

        if (!selectedDate.HasValue
            || !selectedTime.HasValue
            || !selectedDeadline.HasValue)
        {
            await DisplayAlert(
                "Missing Information",
                "Select the event date, time, and RSVP deadline.",
                "OK");

            return;
        }

        DateTime eventDate = selectedDate.Value.Date
            .Add(selectedTime.Value);

        if (eventDate <= DateTime.Now)
        {
            await DisplayAlert(
                "Invalid Date",
                "Choose a future event date and time.",
                "OK");

            return;
        }

        if (selectedDeadline.Value.Date > eventDate.Date)
        {
            await DisplayAlert(
                "Invalid Deadline",
                "The RSVP deadline cannot be after the event date.",
                "OK");

            return;
        }

        await DisplayAlert(
            "Validation Successful",
            "All event fields are complete. Event saving will be added later.",
            "OK");

        await Navigation.PopAsync();
    }
}

// RSVP FORM
public class RsvpPage : ContentPage
{
    private readonly Entry name = new()
    {
        Placeholder = "Full name"
    };

    private readonly Entry email = new()
    {
        Placeholder = "Email",
        Keyboard = Keyboard.Email
    };

    private readonly Entry phone = new()
    {
        Placeholder = "Mobile phone",
        Keyboard = Keyboard.Telephone
    };

    public RsvpPage(EventInfo item)
    {
        Title = "RSVP";

        if (!Session.IsGuest)
        {
            name.Text = Session.Name;
            email.Text = Session.Email;
            phone.Text = Session.Phone;
        }

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("RSVP for an Event"));

        stack.Children.Add(new Label
        {
            Text = item.Name
        });

        Ui.AddField(stack, "Name", name);
        Ui.AddField(stack, "Email", email);
        Ui.AddField(stack, "Mobile Phone", phone);

        stack.Children.Add(
            Ui.Action("Save RSVP", Validate, true));

        stack.Children.Add(
            Ui.Action("Cancel", async () =>
            {
                await Navigation.PopAsync();
            }));

        Content = Ui.Form(stack);
    }

    private async void Validate()
    {
        if (Ui.Missing(name, email, phone))
        {
            await DisplayAlert(
                "Missing Information",
                "Complete all RSVP fields.",
                "OK");

            return;
        }

        await DisplayAlert(
            "Validation Successful",
            "All RSVP fields are complete. Attendance has not been changed. " +
            "RSVP saving will be added later.",
            "OK");

        await Navigation.PopAsync();
    }
}

// INVITATION MANAGEMENT TAB
public class InvitationsPage : ContentPage
{
    public InvitationsPage()
    {
        Title = "RSVPs";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading("Manage Invitations"));

        if (Session.IsGuest)
        {
            stack.Children.Add(new Label
            {
                Text = "Log in to view invitations for events you host."
            });
        }
        else
        {
            foreach (var item in SampleData.Events.Where(
                item => item.Hosting && item.Date >= DateTime.Now))
            {
                stack.Children.Add(
                    Ui.Action(item.Name, async () =>
                    {
                        await Navigation.PushAsync(
                            new InvitationDetailsPage(item));
                    }));
            }
        }

        Content = Ui.Form(stack);
    }
}

// INVITATION DETAILS
public class InvitationDetailsPage : ContentPage
{
    public InvitationDetailsPage(EventInfo item)
    {
        Title = "Invitations";

        var stack = Ui.Stack();

        stack.Children.Add(
            Ui.Heading(item.Name));

        stack.Children.Add(
            Ui.FieldLabel("Pending"));

        foreach (string person in item.Pending)
        {
            stack.Children.Add(new Label
            {
                Text = $"… {person} — Pending",
                TextColor = Colors.Gray
            });
        }

        stack.Children.Add(
            Ui.FieldLabel("Accepted"));

        foreach (string person in item.Accepted)
        {
            stack.Children.Add(new Label
            {
                Text = $"✓ {person} — Accepted",
                TextColor = Color.FromArgb("#218653")
            });
        }

        stack.Children.Add(
            Ui.Action("Event Details", async () =>
            {
                await Navigation.PushAsync(
                    new EventDetailsPage(item));
            }));

        stack.Children.Add(
            Ui.Action("Back", async () =>
            {
                await Navigation.PopAsync();
            }));

        Content = Ui.Form(stack);
    }
}