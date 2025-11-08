using System;
using System.Collections.Generic;

public class HistoryManager
{
    public List<string> Entries { get; private set; } = new List<string>();

    private int maxEntries;

    public HistoryManager()
    {
        maxEntries = SettingsManager.Current.MaxEntries;
    }

    public void Add(string text)
    {
        Entries.Remove(text);         // Remove if already exists
        Entries.Insert(0, text);      // Add to top
        if (Entries.Count > maxEntries)
            Entries.RemoveAt(Entries.Count - 1);

        //Console.WriteLine(maxEntries);
    }
}
