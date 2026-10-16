using WorldEdit.Core;

namespace WorldEdit;

/// <summary>Follows one engine edit job on the tick thread and reports its end to the player. The engine does the work; this polls its state
/// every few ticks and stops when the job is done, failed or cancelled, or when the player has left.</summary>
internal sealed class JobWatch
{
    private const int PollTicks = 5;
    private const int MaxPolls = 4000;  // about 400 seconds; a job that long is reported as still running and left alone

    private readonly Player _player;
    private readonly long _job;
    private readonly string _label;
    private readonly Action<EditJobInfo>? _onDone;
    private ScheduledTask? _task;
    private int _polls;

    private JobWatch(Player player, long job, string label, Action<EditJobInfo>? onDone)
    {
        _player = player;
        _job = job;
        _label = label;
        _onDone = onDone;
    }

    /// <summary>Starts following a job. <paramref name="onDone"/> runs once when the job finishes well (copy and cut use it to keep the handle).</summary>
    public static void Start(Scheduler scheduler, Player player, long job, string label, Action<EditJobInfo>? onDone = null)
    {
        var watch = new JobWatch(player, job, label, onDone);
        watch._task = scheduler.RunEvery(PollTicks, watch.Poll, PollTicks);
    }

    private void Poll()
    {
        if (!_player.IsOnline)
        {
            Stop();
            return;
        }
        EditJobInfo? found = Edit.GetJob(_player, _job);
        if (found is not { } info)
        {
            Stop();
            return;
        }
        _polls++;
        switch (info.State)
        {
            case EditJobState.Done:
                _player.SendMessage($"{_label} done: {Coords.Count(info.Changed)} blocks changed in {info.ElapsedTicks} ticks.{Note(info)}");
                if (info.Clipboard != 0) _onDone?.Invoke(info);
                Stop();
                break;
            case EditJobState.Failed:
                _player.SendMessage($"{_label} stopped after {Coords.Count(info.Changed)} blocks: {info.Message}. Undo restores what it changed.");
                if (info.Clipboard != 0) _onDone?.Invoke(info);
                Stop();
                break;
            case EditJobState.Cancelled:
                _player.SendMessage($"{_label} cancelled after {Coords.Count(info.Changed)} blocks.");
                Stop();
                break;
            default:
                if (_polls >= MaxPolls) Stop();
                break;
        }
    }

    private static string Note(EditJobInfo info) => info.Message.Length == 0 ? string.Empty : $" ({info.Message})";

    private void Stop()
    {
        _task?.Cancel();
        _task = null;
    }
}
