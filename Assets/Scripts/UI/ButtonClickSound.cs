using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the AudioManager's button click whenever this button is clicked.</summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    private void Awake()
    {
        // Hooked up in code, not in the OnClick list: the AudioManager that survives may come from another scene.
        GetComponent<Button>().onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick());
    }
}
