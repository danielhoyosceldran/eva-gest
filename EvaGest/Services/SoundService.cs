using EvaGest.Models;
using Serilog;

namespace EvaGest.Services;

/// <summary>
/// The plan calls for a short, custom cobrar.wav bundled under Resources/Sons. No
/// audio asset pipeline exists yet in this codebase, so this substitutes the nearest
/// built-in equivalent (a short system chime) rather than block Phase 10 on asset
/// production. Swapping in the real .wav later only touches this one class.
/// </summary>
public class SoundService(ISettingsService settings) : ISoundService
{
    public async Task PlayConfirmation()
    {
        // The setting is read inside the try as well. It used to be read before it, so a
        // failed read escaped from here, after the sale had already been saved: the sale
        // dialog stayed open on a charged sale, and Cobrar again charged it twice (E-07).
        try
        {
            if (!await settings.GetBool(ConfigKeys.ConfirmationSound, perDefault: true)) return;

            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            // A missing audio device must never interrupt a sale being saved, but the
            // failure must still be on record instead of vanishing silently.
            Log.Warning(ex, "Could not play confirmation sound");
        }
    }
}
