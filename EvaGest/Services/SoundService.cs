namespace EvaGest.Services;

/// <summary>
/// The plan calls for a short, custom cobrar.wav bundled under Resources/Sons. No
/// audio asset pipeline exists yet in this codebase, so this substitutes the nearest
/// built-in equivalent (a short system chime) rather than block Fase 10 on asset
/// production. Swapping in the real .wav later only touches this one class.
///
/// TODO: read SoConfirmacio from ConfiguracioService once it exists, to honour the
/// "desactivable des de la configuració" requirement (RF-22). Always on for now.
/// </summary>
public class SoundService : ISoundService
{
    public void ReproduirConfirmacio()
    {
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch
        {
            // A missing audio device must never interrupt a sale being saved.
        }
    }
}
