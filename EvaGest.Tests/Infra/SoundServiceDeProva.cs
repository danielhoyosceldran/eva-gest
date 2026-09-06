using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>Silent stand-in that records whether the chime would have played.</summary>
public class SoundServiceDeProva : ISoundService
{
    public int Reproduccions { get; private set; }

    public Task ReproduirConfirmacio()
    {
        Reproduccions++;
        return Task.CompletedTask;
    }
}
