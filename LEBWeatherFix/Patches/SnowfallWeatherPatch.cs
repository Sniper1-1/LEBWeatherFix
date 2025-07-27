using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin;
using VoxxWeatherPlugin.Utils;

namespace LEBWeatherFix.Patches
{
    //handles snowfall fixing
    [HarmonyPatch(typeof(SnowfallVFXManager), nameof(SnowfallVFXManager.PopulateLevelWithVFX))]
    public class SnowfallWeatherPatch
    {
        //this goes through Voxx's PopulateLevelWithVFX method and replaces the call to JingleBells with my own copy of it, since his was compiled against the old game code.
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            LEBWeatherFix.Logger.LogDebug("Transpiler called for Snowfall's PopulateLevelWithVFX");

            var codes = new List<CodeInstruction>(instructions);

            var originalMethod = AccessTools.Method(typeof(SnowfallVFXManager), "JingleBells");
            LEBWeatherFix.Logger.LogDebug($"Found original JingleBells method call: {originalMethod != null}");

            var replacementMethod = AccessTools.Method(typeof(SnowfallWeatherPatch), nameof(SnowfallWeatherPatch.JingleBells_Replacement));
            LEBWeatherFix.Logger.LogDebug($"Found replacement JingleBells_Replacement method: {replacementMethod != null}");

            if (originalMethod == null || replacementMethod == null)
            {
                LEBWeatherFix.Logger.LogError("Failed to find one of the above methods!");
                return codes;
            }

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(originalMethod))
                {
                    LEBWeatherFix.Logger.LogDebug($"Replacing call at index {i} with custom method.");
                    codes[i] = new CodeInstruction(OpCodes.Call, replacementMethod);
                }
            }

            return codes;
        }

        // Must be static to replace instance call
        public static void JingleBells_Replacement(SnowfallVFXManager __instance)
        {
            LEBWeatherFix.Logger.LogDebug("Calling JingleBells_Replacement");

            if (__instance.giftBoxItem == null)
            {
                VoxxWeatherPlugin.Debug.LogError("Gift box item not found in the item database!");
                return;
            }

            VoxxWeatherPlugin.Debug.Log("Merry Christmas!");


            int attempts = 24;
            bool treePlaced = false;
            Vector3 treePosition = Vector3.zero;
            while (attempts-- > 0)
            {
                // Select a random position in the level from RoundManager.Instance.outsideAINodes
                int randomIndex = __instance.SeededRandom?.Next(0, RoundManager.Instance.outsideAINodes.Length) ?? 0;
                Vector3 anchor = RoundManager.Instance.outsideAINodes[randomIndex].transform.position;
                // Sample another random position using navmesh around the anchor where there is at least 10x10m of space
                Vector3 randomPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(anchor, 25f, randomSeed: __instance.SeededRandom);
                randomPosition = RoundManager.Instance.PositionEdgeCheck(randomPosition, 7f);
                if (randomPosition != Vector3.zero)
                {
                    treePosition = randomPosition;
                    treePlaced = true;
                    break;
                }
            }

            if (!treePlaced)
            {
                VoxxWeatherPlugin.Debug.LogDebug("Failed to place a Christmas tree in the level, too many attempts!");
                return;
            }

            Quaternion randomRotation = Quaternion.Euler(0, __instance.SeededRandom?.Next(0, 360) ?? 0f, 0);
            // Spawn a Christmas tree
            _ = GameObject.Instantiate(__instance.christmasTreePrefab!, treePosition, randomRotation);

            // Only host can spawn the presents
            if (!GameNetworkManager.Instance.isHostingGame)
            {
                return;
            }

            // Spawn a gift box for each player in the game. Cap at 4 gifts so users with more than 4 players don't get too many
            int numGifts = Mathf.Min(GameNetworkManager.Instance.connectedPlayers, 4);

            NavMeshHit hit;
            for (int i = 0; i < numGifts; i++)
            {
                int giftValue = __instance.SeededRandom?.Next(1, 24) ?? 1;

                //Spawn gifts in a ring around the tree by sampling the NavMesh around it
                Vector3 giftPosition = treePosition + 2f * new Vector3(Mathf.Cos(i * 2 * Mathf.PI / numGifts), 0, Mathf.Sin(i * 2 * Mathf.PI / numGifts));
                if (NavMesh.SamplePosition(giftPosition, out hit, 2f, NavMesh.AllAreas))
                {
                    __instance.giftBoxItem.SpawnAtPosition(hit.position, giftValue);
                }
            }
        }
    }
}