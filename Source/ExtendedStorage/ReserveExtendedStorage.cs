﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using Harmony;
using UnityEngine;//For the Mathf.min of 3 things

namespace Share_The_Load {
	[StaticConstructorOnStartup]
	public static class ExtendedStoragePatches {
		private const string ES_Building_TypeName = "Building_ExtendedStorage";

		private static GetterHandler getMaxStorage;
		private static GetterHandler getStoredThingTotal;

		public static int Building_ExtendedStorage_GetApparentMaxStorage(Thing t) {
			if (getMaxStorage == null) {
				getMaxStorage = FastAccess.CreateGetterHandler(AccessTools.DeclaredProperty(t.GetType(), "ApparentMaxStorage"));
			}

			return (int) getMaxStorage.Invoke(t);
		}

		public static int Building_ExtendedStorage_GetStoredThingTotal(Thing t) {
			if (getStoredThingTotal == null) {
				getStoredThingTotal = FastAccess.CreateGetterHandler(AccessTools.DeclaredProperty(t.GetType(), "StoredThingTotal"));
			}

			return (int) getStoredThingTotal.Invoke(t);
		}

		public static bool IsES_Storer(Thing t) {
			return t.GetType().Name == ES_Building_TypeName;
		}

		static ExtendedStoragePatches() {
			if (!ModCompatibilityCheck.ExtendedStorageIsActive) return;
			Log.Message($"Share The Load patching with Extended Storage!");

			HarmonyInstance harmony = HarmonyInstance.Create("Uuugggg.rimworld.Share_The_Load-ES.main");
			harmony.Patch(AccessTools.Method(typeof(ReservationManager), "CanReserve"),
			              new HarmonyMethod(typeof(CanReserve_Patch_ES), "Prefix"), null);
			harmony.Patch(AccessTools.Method(typeof(ReservationManager), "Reserve"),
			              new HarmonyMethod(typeof(Reserve_Patch_ES), "Prefix"), null);
			harmony.Patch(AccessTools.Method(typeof(ReservationManager), "Release"),
			              new HarmonyMethod(typeof(Release_Patch_ES), "Prefix"), null);
			harmony.Patch(AccessTools.Method(typeof(ReservationManager), "ReleaseClaimedBy"),
			              new HarmonyMethod(typeof(ReleaseClaimedBy_Patch_ES), "Prefix"), null);
		}

	}

	//[HarmonyPatch(typeof(ReservationManager), "CanReserve")]
	static class CanReserve_Patch_ES {
		//public bool CanReserve(Pawn claimant, LocalTargetInfo target, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool ignoreOtherReservations = false)
		public static bool Prefix(Pawn claimant, LocalTargetInfo target, ref bool __result) {
			if (claimant.IsFreeColonist && target.Cell != LocalTargetInfo.Invalid) {
				if (claimant.Map.thingGrid.ThingsAt(target.Cell).FirstOrDefault(ExtendedStoragePatches.IsES_Storer) is Thing thing) {
					Log.Message($"{claimant} can reserveES? {target.Cell} is {thing}");

					int canDo = ExtendedStoragePatches.Building_ExtendedStorage_GetApparentMaxStorage(thing) - ExtendedStoragePatches.Building_ExtendedStorage_GetStoredThingTotal(thing);
					int expected = ExpectingComp.ExpectedCount(q => q.claimed == thing);

					if (canDo > expected) {
						Log.Message($"{claimant} can reserveES {thing}");
						__result = true;
						return false;
					}
				}
			}

			return true;
		}
	}


	//[HarmonyPatch(typeof(ReservationManager), "Reserve")]
	static class Reserve_Patch_ES {
		//public bool Reserve(Pawn claimant, Job job, LocalTargetInfo target, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null)
		public static bool Prefix(Pawn claimant, Job job, LocalTargetInfo target, ref bool __result) {
			if (claimant.IsFreeColonist && target.Cell != LocalTargetInfo.Invalid
			                            && claimant.Map.thingGrid.ThingsAt(target.Cell).FirstOrDefault(ExtendedStoragePatches.IsES_Storer) is Thing storage
			                            && job.def == JobDefOf.HaulToCell) {
				int canDo = ExtendedStoragePatches.Building_ExtendedStorage_GetApparentMaxStorage(storage) - ExtendedStoragePatches.Building_ExtendedStorage_GetStoredThingTotal(storage);
				if (canDo > 0) {
					int count = job.count;
					Thing deliverThing = job.targetA.Thing;
					ThingDef resource = deliverThing.def;

					Log.Message($"{claimant} reservingES {storage} resource = {resource}({count})");
					Log.Message($"	out of: {canDo}");


					int availableCount = deliverThing.stackCount; // + job.targetQueueA?.Sum(tar => tar.Thing.stackCount) ?? 0;
					//HaulToCell doesn't queue up its reservations, and so we don't know if there are more to get
					count = Mathf.Min(new int[] {count, claimant.carryTracker.MaxStackSpaceEver(resource), availableCount, canDo});

					Log.Message($"{storage} was expecting {resource}(" + ExpectingComp.ExpectedCount(storage, resource) + ")");
					ExpectingComp.Add(claimant, job, storage, resource, count);
					Log.Message($"{storage} now expecting {resource}(" + ExpectingComp.ExpectedCount(storage, resource) + ")");

					__result = true;
					return false;
				}
			}

			return true;
		}
	}

	//[HarmonyPatch(typeof(ReservationManager), "Release")]
	static class Release_Patch_ES {
		//public void Release(LocalTargetInfo target, Pawn claimant, Job job)
		public static void Prefix(LocalTargetInfo target, Pawn claimant, Job job) {
			if (claimant.IsFreeColonist && target.Cell != LocalTargetInfo.Invalid
			                            && claimant.Map.thingGrid.ThingsAt(target.Cell).FirstOrDefault(ExtendedStoragePatches.IsES_Storer) is Thing thing
			                            && job.def == JobDefOf.HaulToCell)
				ExpectingComp.Remove(q => q.claimant == claimant && q.job == job && q.claimed == thing);
		}
	}

	//[HarmonyPatch(typeof(ReservationManager), "ReleaseClaimedBy")]
	static class ReleaseClaimedBy_Patch_ES {
		//public void ReleaseClaimedBy(Pawn claimant, Job job)
		public static void Prefix(Pawn claimant, Job job) {
			if (job.def == JobDefOf.HaulToCell)
				ExpectingComp.Remove(q => q.claimant == claimant && q.job == job);
		}
	}

	//Redundant with normal Constructible
	//[HarmonyPatch(typeof(ReservationManager), "ReleaseAllClaimedBy")]
	//static class ReleaseAllClaimedBy_Patch_ES
	//{
	//	//public void ReleaseAllClaimedBy(Pawn claimant)
	//	public static void Prefix(Pawn claimant)
	//	{
	//		ExpectingComp.Remove(q => q.claimant == claimant);
	//	}
	//}
}
