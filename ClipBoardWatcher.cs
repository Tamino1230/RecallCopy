using System;
using System.Windows.Forms;

public class ClipboardWatcher
{
    public event Action<string> OnNewClipboardText;
    private Timer timer;
    private string lastText = "";
    // Copy

    public ClipboardWatcher()
    {
        timer = new Timer { Interval = 500 };
        timer.Tick += (s, e) =>
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string current = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(current) && current != lastText)
                    {
                        lastText = current;
                        OnNewClipboardText?.Invoke(current);
                    }
                }
            }
            catch { }; // ignore
        };
        timer.Start();
    }
}
