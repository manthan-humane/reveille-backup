using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Tooltip("The exact name of the scene to load when playing.")]
        public string LevelToLoad = "Jungle_level";

        /// <summary>
        /// Call this method from the Play Button's OnClick event.
        /// </summary>
        public void StartGame()
        {
            Debug.Log("StartGame() was called! Attempting to load level: '" + LevelToLoad + "'");
            if (!string.IsNullOrEmpty(LevelToLoad))
            {
                // Unpause the time just in case it was frozen from a pause menu
                Time.timeScale = 1f; 
                SceneManager.LoadScene(LevelToLoad);
            }
            else
            {
                Debug.LogWarning("LevelToLoad is empty. Please specify a scene name in the inspector.");
            }
        }

        /// <summary>
        /// Call this method from the Quit Button's OnClick event.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("QuitGame() called - Exiting application.");
            
#if UNITY_EDITOR
            // This will stop playing the scene if we're in the Unity Editor
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // This will quit the built game
            Application.Quit();
#endif
        }
    }
}
