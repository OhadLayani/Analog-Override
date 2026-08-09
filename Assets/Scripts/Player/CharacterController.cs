using UnityEngine;
using AnalogOverride;
using AnalogOverride.GridSystem;

public class CharacterController : GridEntity
{
    private Animator animator;
    private SpringManager springManager;
    public int stepCounter;
    [SerializeField] private int stepsPerBar = 3;


    private void Awake()
    {
        springManager = SpringManager.Instance;
    }

    protected override void Start()
    {
        base.Start(); // Snaps the player to the grid's center on spawn
        animator = GetComponent<Animator>();
        springManager ??= SpringManager.Instance;
    }

    private void OnEnable()
    {
        springManager ??= SpringManager.Instance;
        if (springManager != null)
        {
            springManager.BarsReachedZero += HandleBarsReachedZero;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (springManager != null)
        {
            springManager.BarsReachedZero -= HandleBarsReachedZero;
        }
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
            if (TryStep(dir))
            {
                stepCounter++;

                if (stepCounter >= stepsPerBar)
                {
                    springManager?.ReduceBars(1);
                    stepCounter = 0;
                }
            }
        }

        // Update the animator based on whether a valid move was accepted
        animator.SetBool("IsMoving", IsMoving);
    }

    private void HandleBarsReachedZero()
    {
        Debug.Log("GAME OVER");
    }
}
