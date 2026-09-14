using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>Silent stand-in that records whether the chime would have played.</summary>
public class TestSoundService : ISoundService
{
    public int Plays { get; private set; }

    public Task PlayConfirmation()
    {
        Plays++;
        return Task.CompletedTask;
    }
}
