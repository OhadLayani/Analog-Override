using UnityEngine;

namespace AnalogOverride.UI
{
    public class SceneNavigationButtons : MonoBehaviour
    {
        public void LoadNextScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadNextScene();
            }
        }
    }
}