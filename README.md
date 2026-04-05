# MyChat Prototype Solution

## Projektüberblick

Dieses Repository enthält einen **Prototypen** für ein wiederverwendbares Chat-Control in .NET.  
Der Fokus liegt auf zwei Fragestellungen:

1. **UI-Vergleich:** Wie lässt sich dieselbe Chat-Funktionalität in drei unterschiedlichen UI-Technologien kapseln?
2. **Realtime-Synchronisation:** Wie verhalten sich drei verschiedene Mechanismen zur Live-Synchronisierung zwischen zwei Clients?

Damit dient das Projekt als technische Vergleichs- und Entscheidungsgrundlage für eine spätere produktive Umsetzung.

---

## Ziel und Zweck der Arbeit

Die Lösung zeigt, wie ein fachlich identisches Chat-Modul unabhängig von der konkreten Darstellungstechnologie verwendet werden kann. Gleichzeitig wird überprüft, welche Realtime-Strategie sich für die Synchronisation von Nachrichten zwischen mehreren Instanzen am besten eignet.

**Konkret wird evaluiert:**

- **3 UI-Technologien**
  - WinForms (nativ)
  - WPF (eingebettet in WinForms via `ElementHost`)
  - WebView2 (HTML/CSS in WinForms)
- **3 Realtime-Varianten**
  - API Polling
  - SignalR
  - Server-Sent Events (SSE)

Das Projekt ist als **Spike/Prototype** aufgebaut: Ziel ist das Verstehen, Vergleichen und Dokumentieren – nicht die vollständige Produktreife.

---

## Architektur auf einen Blick

Die Solution ist in klar getrennte Projekte aufgeteilt:

- `MyChat.Abstractions`  
  Gemeinsame Verträge, Bind-Modelle und Enums (UI-/Sync-Auswahl).
- `MyChat.Factory`  
  Zentrale Erzeugung der Chat-Controls über `MyChatControlFactory`.
- `MyChat.WinForms`  
  Chat-Implementierung für native WinForms.
- `MyChat.Wpf`  
  Chat-Implementierung in WPF, hostbar in WinForms.
- `MyChat.WebView`  
  Chat-Implementierung mit HTML/CSS über WebView2.
- `MyChat.Sync.Service`  
  Backend für Nachrichten und Realtime-Endpunkte (Polling, SignalR, SSE).
- `MyChat.Host.WinForms`  
  Host-Anwendung zur Visualisierung und Interaktion (inkl. Rollenprofilen).
- `MyChat.Startup.Console`  
  Start-/Orchestrierungsprojekt, das Service und zwei Host-Instanzen zusammen startet.

---

## Was beim Start demonstriert wird

Beim Ausführen des Startup-Projekts wird ein kompletter Testablauf aufgesetzt:

1. Start der Konsole (`MyChat.Startup.Console`)
2. Auswahl der Synchronisation per Eingabe:
   - `1` = API Polling
   - `2` = SignalR
   - `3` = SSE
3. Automatischer Start von:
   - `MyChat.Sync.Service`
   - `MyChat.Host.WinForms` als Rolle **Supporter**
   - `MyChat.Host.WinForms` als Rolle **Applikationsentwickler**

Dadurch können zwei parallel laufende Clients direkt miteinander verglichen werden.

> Der aktuelle Spike synchronisiert primär **Textnachrichten** in Echtzeit.

---

## Realtime-Mechanismen im Vergleich

- **API Polling** (`/api/messages`)  
  Client fragt in Intervallen neue Daten ab.
- **SignalR** (`/hubs/chat`)  
  Bidirektionale, ereignisbasierte Kommunikation in (nahezu) Echtzeit.
- **SSE** (`/api/messages/stream`)  
  Serverseitiger Event-Stream zum Client über HTTP.

Die Umschaltung ermöglicht einen direkten Vergleich hinsichtlich Integrationsaufwand, Aktualisierungsverhalten und wahrgenommener Reaktionszeit.

---

## Voraussetzungen

- .NET SDK (Version gemäss Projektdateien/Solution)
- Windows-Umgebung für WinForms/WPF/WebView2-Szenario
- Für `MyChat.WebView`: NuGet-Paket `Microsoft.Web.WebView2`
- Für SignalR-Client im Host: `Microsoft.AspNetCore.SignalR.Client`

---

## Projekt starten

### Variante A (empfohlen): über Startup-Konsole

1. Solution in Visual Studio öffnen.
2. `MyChat.Startup.Console` als Startprojekt setzen.
3. Anwendung starten.
4. In der Konsole `1`, `2` oder `3` eingeben, um die Sync-Technologie zu wählen.
5. Beobachten, wie Service und zwei Host-Fenster automatisch starten und Nachrichten synchronisieren.

### Variante B: manuell

1. `MyChat.Sync.Service` starten.
2. Zwei Instanzen von `MyChat.Host.WinForms` starten (unterschiedliche Rollen wählen).
3. In den Hosts dieselbe Sync-Technologie aktivieren.
4. Nachrichtenaustausch testen.

---

## Beispiel zur Verwendung der Factory

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

### Eigenes WebView2-Control registrieren (optional)

```csharp
MyChatControlFactory.UseCustomWebView2Component<MyCompanyWebView2>();
var webChat = MyChatControlFactory.Create(ChatUiTechnology.WebView2);
```

Zurücksetzen auf Standardkomponente:

```csharp
MyChatControlFactory.ResetCustomWebView2Component();
```

---

## Ergebnis / Nutzen

Dieses Repository dokumentiert reproduzierbar,

- wie ein UI-unabhängiges Chat-Control strukturiert werden kann,
- wie drei Darstellungsvarianten im selben technischen Rahmen vergleichbar gemacht werden,
- und wie sich drei Realtime-Ansätze unter identischen Bedingungen praktisch verhalten.

Damit bildet das Projekt eine nachvollziehbare Grundlage für Architekturentscheidungen.
