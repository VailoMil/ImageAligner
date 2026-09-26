namespace ImageAligner.Core.Events;

public sealed class AlignProgressEventArgs : EventArgs
{
    public string Stage { get; }
    public double Progress { get; }

    public AlignProgressEventArgs(string stage, double progress)
    {
        Stage = stage;
        Progress = progress;
    }
}