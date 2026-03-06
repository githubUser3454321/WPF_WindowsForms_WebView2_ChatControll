# MyChat Prototype Solution

Diese Solution demonstriert ein wiederverwendbares Chat-Control mit gemeinsamer API in drei UI-Technologien:

- `MyChat.WinForms`: native WinForms
- `MyChat.Wpf`: WPF in WinForms (`ElementHost`)
- `MyChat.WebView`: HTML/CSS in WebView2 in WinForms

## Projekte

- `MyChat.Abstractions`: gemeinsame API (`IMyChatBindable`, Bind-Modell, Enum)
- `MyChat.Factory`: zentraler Einstieg via `MyChatControlFactory`
- `MyChat.Host.WinForms`: Demo-Host, der alle Varianten laden kann

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

## Hinweise

- Für `MyChat.WebView` wird das NuGet-Paket `Microsoft.Web.WebView2` benötigt.
- Das Demo-Form erlaubt den direkten Technologie-Wechsel per ComboBox.
