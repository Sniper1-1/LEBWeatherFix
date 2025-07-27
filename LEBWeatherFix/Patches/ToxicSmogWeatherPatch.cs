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
    //handles toxic smog fixing
    [HarmonyPatch(typeof(ToxicSmogVFXManager), nameof(ToxicSmogVFXManager.SpawnFumes))]
    public class ToxicSmogWeatherPatch
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
            LEBWeatherFix.Logger.LogDebug($"Found original GetValidSpawnPosition method call: {originalMethod != null}");

            var replacementMethod = AccessTools.Method(typeof(ToxicSmogWeatherPatch), nameof(ToxicSmogWeatherPatch.GetValidSpawnPosition_Replacement));
            LEBWeatherFix.Logger.LogDebug($"Found replacement GetValidSpawnPosition_Replacement method: {replacementMethod != null}");
            
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
                        
            Vector3 potentialPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(
                objectPosition, __instance.spawnRadius, navHit, random, NavMesh.AllAreas);

            if (__instance.IsPositionValid(potentialPosition, blockedPositions))
            {
                return potentialPosition;
            }

            return Vector3.zero;
        }
    }
}
