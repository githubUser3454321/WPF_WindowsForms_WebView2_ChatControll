# MyChat Prototype Solution

Diese Solution demonstriert ein wiederverwendbares Chat-Control mit gemeinsamer API in drei UI-Technologien:

- `MyChat.WinForms`: native WinForms
- `MyChat.Wpf`: WPF in WinForms (`ElementHost`)
- `MyChat.WebView`: HTML/CSS in WebView2 in WinForms

## Projekte

- `MyChat.Abstractions`: gemeinsame API (`IMyChatBindable`, Bind-Modell, UI- und Sync-Enums)
- `MyChat.Factory`: zentraler Einstieg via `MyChatControlFactory`
- `MyChat.Host.WinForms`: Demo-Host mit Rollenprofilen und Sync-Optionen
- `MyChat.Sync.Service`: Sync-Backend für den Spike (API Polling, SignalR, SSE)
- `MyChat.Startup.Console`: Startprojekt, das Service + 2 Host-Instanzen orchestriert

## Neuer Realtime-Sync-Spike

Das Startup-Projekt führt einen kleinen Validierungs-Spike aus:

1. Konsole starten (`MyChat.Startup.Console`)
2. Per Eingabe `1`, `2` oder `3` Sync-Technologie wählen
3. Es werden automatisch gestartet:
   - `MyChat.Sync.Service`
   - `MyChat.Host.WinForms` als `Supporter`
   - `MyChat.Host.WinForms` als `Applikationsentwickler`

Die Host-App startet standardmäßig in der WebView2-Variante. Neben der UI-Technologie gibt es CheckBoxen für die aktive Realtime-Synchronisation:

- **API Polling** (`/api/messages`)
- **SignalR** (`/hubs/chat`)
- **Server Sent Events (SSE)** (`/api/messages/stream`)

Der Spike synchronisiert zunächst nur **Textnachrichten**.

## Beispielnutzung

```csharp
var chat = MyChatControlFactory.Create(ChatUiTechnology.Wpf);
var control = MyChatControlFactory.AsControl(chat);

control.Location = new Point(0, 29);
control.Margin = new Padding(0);
control.Size = new Size(690, 388);

chat.HeaderHeight = 19;
chat.RowHeight = 24;
chat.BindValues(new ChatBindModel
{
    ObjectType = "Invoice",
    RecordId = "123",
    CurrentUser = "Matthias"
});
chat.ReloadRequested += (_, _) => { /* reload */ };
```

### Eigenes `WebView2`-Control bereitstellen

Falls im Host-Projekt ein abgeleitetes `WebView2`-Control verwendet werden soll, kann dieses vor dem Erstellen des Chat-Controls registriert werden:

```csharp
MyChatControlFactory.UseCustomWebView2Component<MyCompanyWebView2>();
var webChat = MyChatControlFactory.Create(ChatUiTechnology.WebView2);
```

Zurücksetzen auf das Standard-`WebView2`:

```csharp
MyChatControlFactory.ResetCustomWebView2Component();
```

## Hinweise

- Für `MyChat.WebView` wird das NuGet-Paket `Microsoft.Web.WebView2` benötigt.
- Für SignalR im Host wird `Microsoft.AspNetCore.SignalR.Client` verwendet.
