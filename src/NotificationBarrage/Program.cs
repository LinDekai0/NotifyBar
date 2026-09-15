namespace NotificationBarrage;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\NotificationBarrage", out var created);
        if (!created) return;
        try { new App(args).Run(); }
        finally { mutex.ReleaseMutex(); }
    }
}
