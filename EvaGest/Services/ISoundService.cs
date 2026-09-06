namespace EvaGest.Services;

public interface ISoundService
{
    /// <summary>Short chime after a sale is taken (RF-22). Does nothing when the user
    /// has turned the sound off in Configuració, which is why it has to read settings
    /// and therefore why it is async.</summary>
    Task ReproduirConfirmacio();
}
