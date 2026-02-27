// ============================================================================
// SceneLoader.cs
// Namespace: Condemned.Core
// Description: Async scene management with loading screen support.
//              All scene transitions go through here — never load scenes directly.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Condemned.Core
{
    public class SceneLoader : Singleton<SceneLoader>
    {
        // ─── Scene Name Constants ─────────────────────────────────────────────
        // Keep these in sync with your Build Settings scene list.

        public static class Scenes
        {
            public const string Bootstrap  = "Bootstrap";
            public const string MainMenu   = "MainMenu";
            public const string Lobby      = "Lobby";
            public const string Game       = "Game";
            public const string EndScreen  = "EndScreen";
        }

        // ─── State ────────────────────────────────────────────────────────────

        public bool IsLoading { get; private set; } = false;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Load a scene asynchronously. Optionally show a loading screen.
        /// </summary>
        public void LoadScene(string sceneName, bool showLoadingScreen = true)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading. Ignoring request for {sceneName}.");
                return;
            }

            StartCoroutine(C_LoadScene(sceneName, showLoadingScreen));
        }

        // ─── Internals ────────────────────────────────────────────────────────

        private IEnumerator C_LoadScene(string sceneName, bool showLoadingScreen)
        {
            IsLoading = true;

            // TODO (M9): Fade in loading screen UI here
            if (showLoadingScreen)
            {
                // LoadingScreen.Instance.Show();
                yield return null; // placeholder frame
            }

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                // TODO (M9): Update loading screen progress bar
                // LoadingScreen.Instance.SetProgress(op.progress);
                yield return null;
            }

            // Scene is loaded but not yet active — good point to do final setup
            yield return new WaitForSeconds(0.1f); // brief pause for visual smoothness

            op.allowSceneActivation = true;

            yield return new WaitUntil(() => op.isDone);

            // TODO (M9): Fade out loading screen UI here
            // LoadingScreen.Instance.Hide();

            IsLoading = false;
        }
    }
}
