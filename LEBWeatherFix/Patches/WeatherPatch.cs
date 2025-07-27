using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using VoxxWeatherPlugin.Weathers;

namespace LEBWeatherFix.Patches
{
    [HarmonyPatch(typeof(ToxicSmogVFXManager), nameof(ToxicSmogVFXManager.SpawnFumes))]
    public class WeatherPatch
    {
        //This goes through Voxx's SpawnFumes method and replaces the call to GetValidSpawnPosition with my own copy of it, since his was compiled against the old game code.
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            LEBWeatherFix.Logger.LogDebug("Transpiler called for SpawnFumes");

            var codes = new List<CodeInstruction>(instructions);

            var originalMethod = AccessTools.Method(typeof(ToxicSmogVFXManager), "GetValidSpawnPosition",
                new[]
                {
                    typeof(Vector3),
                    typeof(List<Vector3>),
                    typeof(NavMeshHit).MakeByRefType(),
                    typeof(System.Random)
                });
            LEBWeatherFix.Logger.LogDebug($"Found original method: {originalMethod != null}");

            var replacementMethod = AccessTools.Method(typeof(WeatherPatch), nameof(WeatherPatch.GetValidSpawnPosition_Replacement));
            LEBWeatherFix.Logger.LogDebug($"Found replacement method: {replacementMethod != null}");
            
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
                    codes[i] = new CodeInstruction(OpCodes.Call, replacementMethod); // static method, so use Call
                }
            }

            return codes;
        }

        // Must be static to replace instance call
        public static Vector3 GetValidSpawnPosition_Replacement(
            ToxicSmogVFXManager __instance,
            Vector3 objectPosition,
            List<Vector3> blockedPositions,
            ref NavMeshHit navHit,
            System.Random random)
        {
            LEBWeatherFix.Logger.LogDebug("Calling GetValidSpawnPosition_Replacement");

            var roundManager = RoundManager.Instance;
            if (roundManager == null)
            {
                LEBWeatherFix.Logger.LogWarning("RoundManager.Instance was null during spawn calculation.");
                return Vector3.zero;
            }

            Vector3 potentialPosition = roundManager.GetRandomNavMeshPositionInBoxPredictable(
                objectPosition, __instance.spawnRadius, navHit, random, NavMesh.AllAreas);

            return __instance.IsPositionValid(potentialPosition, blockedPositions)
                ? potentialPosition
                : Vector3.zero;
        }
    }
}
