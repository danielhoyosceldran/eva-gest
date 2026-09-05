using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>No-op stand-in: tests must never depend on an actual audio device.</summary>
public class SoundServiceDeProva : ISoundService
{
    public void ReproduirConfirmacio() { }
}
