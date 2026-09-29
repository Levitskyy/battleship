using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restarts the battle session by tearing down the persistent
/// objects created by BattleSceneSetup and reloading the scene.
///
/// Lives on its own GameObject on purpose: the coroutine must
/// survive Destroy(gameObject) on BattleSceneSetup, otherwise it
/// would be cancelled before the scene load happens.
/// </summary>
public class BattleRestartHost : MonoBehaviour
{
    public static void Begin(
        BattleSceneSetup setup,
        GameObject networkObject,
        GameObject eventSystemObject,
        GameObject canvasObject)
    {
        GameObject host =
            new GameObject("BattleRestartHost");

        DontDestroyOnLoad(host);

        host.AddComponent<BattleRestartHost>()
            .Run(
                setup,
                networkObject,
                eventSystemObject,
                canvasObject);
    }

    private void Run(
        BattleSceneSetup setup,
        GameObject networkObject,
        GameObject eventSystemObject,
        GameObject canvasObject)
    {
        StartCoroutine(
            RestartRoutine(
                setup,
                networkObject,
                eventSystemObject,
                canvasObject));
    }

    private IEnumerator RestartRoutine(
        BattleSceneSetup setup,
        GameObject networkObject,
        GameObject eventSystemObject,
        GameObject canvasObject)
    {
        Debug.Log(
            "[BattleRestart] Restarting the battle session.");

        if (setup != null)
            setup.ShutdownForRestart();

        DestroySafe(networkObject);
        DestroySafe(eventSystemObject);
        DestroySafe(canvasObject);

        BattleSceneSetup.AllowBootstrap();

        yield return null;

        BattleBootstrap.EnsureSetup();

        Scene scene =
            SceneManager.GetActiveScene();

        if (scene.IsValid() && scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(
                scene.buildIndex,
                LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(
                scene.name,
                LoadSceneMode.Single);
        }

        yield return null;

        Destroy(gameObject);
    }

    private static void DestroySafe(GameObject target)
    {
        if (target == null)
            return;

        Destroy(target);
    }
}
