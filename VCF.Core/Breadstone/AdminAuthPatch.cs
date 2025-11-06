using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Breadstone;

[HarmonyPatch(typeof(AdminAuthSystem), nameof(AdminAuthSystem.OnUpdate))]
internal class AdminAuthPatch
{
	static List<Entity> authing = [];
	public static void Prefix(AdminAuthSystem __instance)
	{
		var entities = __instance._Query.ToEntityArray(Allocator.Temp);
		foreach (var entity in entities)
		{
			var fromCharacter = __instance.EntityManager.GetComponentData<FromCharacter>(entity);
			authing.Add(fromCharacter.User);
		}
	}

	public static void Postfix(AdminAuthSystem __instance)
	{
		foreach (var entity in authing)
		{
			var user = __instance.EntityManager.GetComponentData<User>(entity);
			if (user.IsAdmin)
			{
				Task.Run(async () => await ThunderstoreVersionChecker.CheckAllPluginVersionsAsync(entity));
			}
		}
		authing.Clear();
	}
}
