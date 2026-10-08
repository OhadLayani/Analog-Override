namespace AnalogOverride.Entities
{
    /// <summary>
    /// Implement on anything that reacts to a spinning DeskLamp's head passing through it (see
    /// DeskLamp.SweepCheck). Called once per spin per collider, and only for things that opt in by
    /// implementing this — an ordinary collider the lamp sweeps into is just reported, not affected.
    /// </summary>
    public interface ISweepable
    {
        void SweptBy(DeskLamp lamp);
    }
}
