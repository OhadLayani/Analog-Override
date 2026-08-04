using UnityEngine;
using AnalogOverride.GridSystem; 
namespace Cainos.PixelArtTopDown_Basic
{
    public class TopDownCharacterController : GridEntity
    {
        private Animator animator;

        protected override void Start()
        {
            base.Start(); // Snaps the player to the grid's center on spawn
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // If the visual slide from the last step is still playing, ignore new input
            if (IsMoving)
            {
                animator.SetBool("IsMoving", true);
                return;
            }

            Vector2Int dir = Vector2Int.zero;

            // Determine discrete grid direction based on input
            if (Input.GetKey(KeyCode.A))
            {
                dir = Vector2Int.left;
                animator.SetInteger("Direction", 3);
            }
            else if (Input.GetKey(KeyCode.D))
            {
                dir = Vector2Int.right;
                animator.SetInteger("Direction", 2);
            }
            else if (Input.GetKey(KeyCode.W))
            {
                dir = Vector2Int.up;
                animator.SetInteger("Direction", 1);
            }
            else if (Input.GetKey(KeyCode.S))
            {
                dir = Vector2Int.down;
                animator.SetInteger("Direction", 0);
            }

            // If a directional key is pressed, attempt to step on the grid
            if (dir != Vector2Int.zero)
            {
                // TryStep handles checking for walls, pushing blocks, and starting the movement coroutine
                TryStep(dir);
            }

            // Update the animator based on whether a valid move was accepted
            animator.SetBool("IsMoving", IsMoving);
        }
    }
}