using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using System.Threading.Tasks;
using Unity.Collections;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Breadstone;

[HarmonyPatch(typeof(AdminAuthSystem), nameof(AdminAuthSystem.OnUpdate))]
internal class AdminAuthPatch
{
	public static void Prefix(AdminAuthSystem __instance)
	{
		var entities = __instance._Query.ToEntityArray(Allocator.Temp);
		foreach (var entity in entities)
		{
			var fromCharacter = __instance.EntityManager.GetComponentData<FromCharacter>(entity);
			Task.Run(async() => await ThunderstoreVersionChecker.CheckAllPluginVersionsAsync(fromCharacter.User));
		}
	}
}
