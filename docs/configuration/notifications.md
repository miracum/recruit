# Notifications

The screening list tells study team members when patients are newly recommended for a trial they follow. Each new
recommendation shows up in the app's notification list and is sent by email, on a schedule each user chooses.

## Subscribing to a trial

Open a trial's screening list and click the bell button next to its title. Switch on notifications and pick how
often you want an email:

| Frequency | When the email is sent                            |
| --------- | ------------------------------------------------- |
| Instant   | A few minutes after a new recommendation          |
| Daily     | Once a day at the time you choose                 |
| Weekly    | Once a week on the day and at the time you choose |
| Monthly   | On the 1st of every month at 08:00                |

Times are in your own time zone, as reported by your browser when you subscribe, and emails can arrive up to 5
minutes after the chosen time. The frequency only affects email: new recommendations show up in the notification list
straight away, whatever you pick. To stop notifications for a trial, switch them off again in the same place.

Subscribing needs an email address on your account, which the screening list takes from your sign-in.

## The notification list

**Notifications** in the sidebar lists the patients newly recommended for the trials you're subscribed to. The badge
next to it counts the unread ones.

![The notification list with five unread notifications, grouped into the DEMO-BRONCHITIS and FIRST trials](../_img/screenshots/notifications/notification-list-light.png#only-light)
![The notification list with five unread notifications, grouped into the DEMO-BRONCHITIS and FIRST trials](../_img/screenshots/notifications/notification-list-dark.png#only-dark)

- **Unread** and **Read** tabs: marking a notification as read moves it to Read, and marking it as unread moves it back.
- Notifications are grouped by trial. **Mark all as read** clears everything, and **Mark trial as read** next to a
  trial clears only that trial's notifications. Use the checkboxes to mark several at once.
- Clicking a notification marks it as read and opens the trial's screening list with that patient's details open.
  After you close them, the patient's row stays highlighted at the top of the list.

If you lose access to a trial, its notifications are hidden from the list.

## Emails

You get one email per trial, covering every recommendation since the previous one. It only says how many new
candidates were found and for which trial, never who they are, so no patient data leaves the screening list by email.

The button in the email opens the trial's screening list, after signing you in if needed, and marks the notifications
that email was about as read in the app. It only does so for the person the email was sent to: a forwarded email still
opens the screening list, but doesn't change anyone's notifications.

Emails are only sent while you still have access to the trial. If your access is revoked, pending emails for it are
dropped, and your subscription takes effect again if access is restored.

!!! note "Admins need an access grant to receive emails"

    Admins can see every trial and get every notification in the app, but emails are sent in the background, where the
    screening list can't tell who is an admin. To receive emails for a trial, an admin needs an access grant for it like
    any other user.

    This doesn't apply when authentication is disabled (`Auth__Enabled=false`), as in the demo: there, everyone is
    treated as the admin, and emails are sent without any grant.

## How new recommendations are detected

Every minute, the screening list compares each trial's screening list on the FHIR server with the version it saw last
time, using the FHIR server's version history. Every patient added since then becomes a notification for each
subscriber of that trial. A patient who was removed from a screening list and later added back counts as new again.

The first time the screening list sees a trial's screening list, it notes the current patients without notifying
anyone, so installing recruIT or creating a study doesn't produce a notification for every existing candidate.

This needs a FHIR server that keeps the version history of resources.

## Setting up email

Configure the mail server and the app's public URL with the `Notifications__Email__*` settings, see
[Configuration options](options.md#notifications). `PublicBaseUrl` must be the URL your users open the screening list
at, since every link in the emails is built from it.

To check the mail settings, sign in as an admin and open **Notification Test** in the sidebar. It sends a sample email
through the configured mail server straight away. If sending fails, the page shows the full error and the conversation
with the mail server, with credentials hidden.

If a notification email can't be sent, the screening list retries it every 5 minutes, and gives up after 30 days.

## Trying it out in the demo

The [demo installation](../getting-started/installation.md) sends all emails to MailDev, a mock mail server you can
open at <http://127.0.0.1:32025>. Authentication is disabled in the demo, so subscribing to the demo study from its
screening list is all you need.

The demo's patient data never changes, though. Its screening list gets all of its candidates in the query module's
first run, which the screening list only notes, as described [above](#how-new-recommendations-are-detected), so no
notifications turn up on their own. To see an email arrive, use the **Notification Test** page.
