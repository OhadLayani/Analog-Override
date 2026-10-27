/// <summary>
/// Implement on a scene object with state beyond its grid cell that a room snapshot should keep
/// (see RoomSnapshot): a door's open/closed, a roomba's heading. The state is a plain string, so the
/// snapshot never needs to know what any of it means. Cells of pushable/movable GridEntities are
/// saved by RoomSnapshot itself and don't need this.
/// </summary>
public interface ISaveable
{
    string CaptureState();
    void RestoreState(string state);
}
