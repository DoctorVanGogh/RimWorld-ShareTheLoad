﻿using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace Share_The_Load
{
	class Settings : ModSettings
	{
		public bool deliverAsMuchAsYouCan = false;

		public static Settings Get()
		{
			return LoadedModManager.GetMod<Share_The_Load.Mod>().GetSettings<Settings>();
		}

		public void DoWindowContents(Rect wrect)
		{
			var options = new Listing_Standard();
			options.Begin(wrect);
			
			options.CheckboxLabeled("Deliver resources even if there's not enough to finish the building", ref deliverAsMuchAsYouCan);
			options.Gap();

			options.End();
		}
		
		public override void ExposeData()
		{
			Scribe_Values.Look(ref deliverAsMuchAsYouCan, "deliverAsMuchAsYouCan", false);
		}
	}
}