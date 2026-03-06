using System.ComponentModel;
using System.Windows.Forms.Integration;
using MyChat.Abstractions;

namespace MyChat.Wpf;

public sealed class MyChatWpfControl : System.Windows.Forms.UserControl, IMyChatBindable
{
    private readonly ElementHost _host;
    private readonly ChatView _chatView;
    private int _headerHeight = 32;
    private int _rowHeight = 24;

    public MyChatWpfControl()
    {
        _chatView = new ChatView();
        _chatView.SetReloadHandler();
        _chatView.ReloadRequested += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);

        _host = new ElementHost
        {
            Dock = DockStyle.Fill,
            Child = _chatView
        };

        Controls.Add(_host);
        ApplyVisualSettings();
    }

    [Browsable(true)]
    [DefaultValue(32)]
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            _headerHeight = Math.Max(16, value);
            ApplyVisualSettings();
        }
    }

    [Browsable(true)]
    [DefaultValue(24)]
    public int RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = Math.Max(16, value);
            ApplyVisualSettings();
        }
    }

    public ChatBindModel? BoundModel { get; private set; }

    public event EventHandler? ReloadRequested;

    public void BindValues(ChatBindModel model)
    {
        BoundModel = model;
        ApplyVisualSettings();
    }

    public void AddMessage(ChatMessage message)
    {
        _chatView.AddMessage(message);
    }

    private void ApplyVisualSettings()
    {
        var label = BoundModel is null
            ? "Chat (WPF)"
            : $"Chat (WPF) | {BoundModel.ObjectType}/{BoundModel.RecordId} | User: {BoundModel.CurrentUser}";

        _chatView.ConfigureHeader(_headerHeight, label);
        _chatView.ConfigureRowHeight(_rowHeight);
        _chatView.ConfigureCurrentUser(BoundModel?.CurrentUser ?? string.Empty);
        _chatView.SetInputPlaceholder("Nachricht ...");
    }
}
