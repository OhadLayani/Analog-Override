using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StageGoal : MonoBehaviour
{
    private bool triggered;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (triggered) return;

        // Check if the colliding object is the player
        CharacterController player = collision.GetComponent<CharacterController>();
        if (player == null) return;

        triggered = true;

        if (StageOverScreen.Instance != null)
        {
            StageOverScreen.Instance.Show();
        }
    }
}
