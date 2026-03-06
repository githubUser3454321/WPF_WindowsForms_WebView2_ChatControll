using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using MyChat.Abstractions;
using MyChat.WebView;
using MyChat.WinForms;
using MyChat.Wpf;

namespace MyChat.Factory;

public static class MyChatControlFactory
{
    public static void UseCustomWebView2Component<TWebView2>() where TWebView2 : WebView2, new()
    {
        MyChatWebViewControl.UseWebViewFactory<TWebView2>();
    }

    public static void ResetCustomWebView2Component()
    {
        MyChatWebViewControl.ResetWebViewFactory();
    }

    public static IMyChatBindable Create(ChatUiTechnology technology)
    {
        return technology switch
        {
            ChatUiTechnology.WinForms => new MyChatWinFormsControl(),
            ChatUiTechnology.Wpf => new MyChatWpfControl(),
            ChatUiTechnology.WebView2 => new MyChatWebViewControl(),
            _ => throw new ArgumentOutOfRangeException(nameof(technology), technology, "Unsupported technology")
        };
    }

    public static Control AsControl(IMyChatBindable bindable)
    {
        return bindable as Control ?? throw new InvalidOperationException("Chat implementation must inherit from WinForms Control.");
    }
}
