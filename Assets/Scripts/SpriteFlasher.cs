using UnityEngine;

/// <summary>
/// Pulses the alpha of every SpriteRenderer on this object and its children for a set time, then
/// puts their colours back exactly as they were. A fade in alpha shows on any sprite, however
/// dark — a colour tint would do nothing on a black one. Used for a roomba sitting at its dock and
/// for the player flashing after a hit; whoever needs it just calls Flash (the component adds itself
/// where needed, so there's nothing to set up on a prefab).
/// </summary>
public class SpriteFlasher : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float remaining;
    private float clock;
    private float flashesPerSecond;
    private float minAlpha;

    public bool IsFlashing => remaining > 0f;

    /// <summary>
    /// Flashes for `seconds`. Calling this while already flashing extends the flash if the new
    /// time is longer, and never cuts one short. `minimumAlpha` is how faint the dim end of each
    /// flash gets (0 = invisible, 1 = no visible flash).
    /// </summary>
    public void Flash(float seconds, float flashesPerSecond = 3f, float minimumAlpha = 0.25f)
    {
        if (seconds <= 0f) return;

        if (!IsFlashing)
        {
            // Captured fresh at the start of each flash, not once at startup, so it restores
            // whatever colours the sprites have NOW rather than whatever they had at load.
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].color;
            }

            clock = 0f;
        }

        remaining = Mathf.Max(remaining, seconds);
        this.flashesPerSecond = flashesPerSecond;
        minAlpha = minimumAlpha;
    }

    private void Update()
    {
        if (!IsFlashing) return;

        remaining -= Time.deltaTime;
        clock += Time.deltaTime;

        if (remaining <= 0f)
        {
            Restore();
            return;
        }

        var pulse = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f * flashesPerSecond);
        var factor = Mathf.Lerp(minAlpha, 1f, pulse);

        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            var c = originalColors[i];
            renderers[i].color = new Color(c.r, c.g, c.b, c.a * factor);
        }
    }

    private void OnDisable()
    {
        if (!IsFlashing) return;

        remaining = 0f;
        Restore();
    }

    private void Restore()
    {
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = originalColors[i];
            }
        }
    }
}
