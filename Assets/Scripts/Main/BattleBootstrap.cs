using UnityEngine;

public static class BattleBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSetup()
    {
        EnsureSetup();
    }

    public static BattleSceneSetup EnsureSetup()
    {
        BattleSceneSetup existing = Object.FindAnyObjectByType<BattleSceneSetup>();

        if (existing != null)
            return existing;

        GameObject host = new GameObject("BattleSceneSetup");

        return host.AddComponent<BattleSceneSetup>();
    }
}
