using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

namespace FluidsVulkan;

public sealed class ProgressBar : IDisposable
{
    private const string Animation = @"|/-\";

    private readonly TimeSpan _animationInterval =
        TimeSpan.FromSeconds(1.0 / 10);

    private readonly int _blocks;
    private readonly Timer _timer;
    private float _progress;
    private int _stringLength;
    private int _tick;

    public ProgressBar(int blocks)
    {
        _blocks = blocks;
        _timer = new Timer(_animationInterval);
        _timer.AutoReset = true;
        _timer.Enabled = true;
        _timer.Elapsed += UpdateText;
    }

    public void Dispose()
    {
        Thread.Sleep(_animationInterval);
        _timer.Dispose();
    }

    public void Update(float progress)
    {
        _progress = progress;
    }

    private void UpdateText(object sender, ElapsedEventArgs e)
    {
        var progressBlockCount = (int)Math.Floor(_progress * _blocks);
        var text = string.Format("[{0}{1}] {2,3}% {3}",
            new string('#',
                progressBlockCount),
            new string('-',
                _blocks - progressBlockCount),
            Math.Ceiling(100 * _progress),
            Animation[
                _tick]);
        var stringBuilder = new StringBuilder();
        stringBuilder.Append('\b', _stringLength);
        stringBuilder.Append(text);
        _stringLength = text.Length;
        _tick = ++_tick % Animation.Length;
        Console.Write(stringBuilder);
    }
}