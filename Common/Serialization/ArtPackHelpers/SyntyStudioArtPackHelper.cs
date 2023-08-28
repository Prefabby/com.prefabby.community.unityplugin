/*
	Prefabby Unity plugin
    Copyright (C) 2023  Matthias Gall <matt@prefabby.com>

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

namespace Prefabby
{

class SyntyStudioArtPackHelper : IArtPackHelper
{

	public string CreatePrefabHint(GameObject go, SerializedGameObject sgo, string prefabPath, string prefabName, GameObject prefab, PrefabDictionaryItem item)
	{
		string hint = "";

		if (prefabName.StartsWith("SM_Chr_") || prefabName.StartsWith("Character_"))
		{
			if (prefabPath.EndsWith("/FixedScaleCharacters") || prefabPath.EndsWith("/FixedScaleCharacter") || prefabPath.EndsWith("/ScaleFixedCharacters"))
			{
				hint += "FixedScale";
			}
		}

		return string.IsNullOrEmpty(hint) ? null : hint;
	}

	public string DeterminePrefabPath(PrefabReference prefab, ArtPackHint hint)
	{
		Assert.IsNotNull(prefab);
		Assert.IsNotNull(hint);

		string name = prefab.name;

		DebugUtils.Log(DebugContext.Deserialization, $"Determining path for Synty Studios prefab {name}...");

		string fullPath = $"{hint.path}/";
		if (hint.key.EndsWith("/Heist"))
		{
			// Heist uses Prefab instead of Prefabs
			fullPath += "Prefab/";
		}
		else
		{
			fullPath += "Prefabs/";
		}

		// Zombies are directly in Prefabs folder and start with Zombie_
		if (name.StartsWith("SM_Chr_") || name.StartsWith("Character_"))
		{
			if (hint.key.EndsWith("/GangWarfare"))
			{
				// Gang warfare pack uses Character instead of Characters
				fullPath += "/Character";
			}
			else if (!hint.key.EndsWith("/CityCharacters") && !hint.key.EndsWith("/FantasyCharacters"))
			{
				// City characters pack and Fantasy characters packs don't have a Characters subdirectory
				fullPath += "Characters/";
			}

			// Horror Mansion has another Characters subdirectory but that shouldn't matter

			// Check if the hint contains "FixedScale" to identify fixed scale character prefabs
			if (prefab.hint != null && prefab.hint.Contains("FixedScale"))
			{
				if (hint.key.EndsWith("/City") ||
					hint.key.EndsWith("/FantasyCharacters") ||
					hint.key.EndsWith("/GangWarfare") ||
					hint.key.EndsWith("/Heist") ||
					hint.key.EndsWith("/Knights") ||
					hint.key.EndsWith("/Samurai") ||
					hint.key.EndsWith("/SciFiCity") ||
					hint.key.EndsWith("/War") ||
					hint.key.EndsWith("/WesternFrontier")
				)
				{
					fullPath += "FixedScaleCharacters/";
				}
				else if (hint.key.EndsWith("/Pirates"))
				{
					fullPath += "FixedScaleCharacter/";
				}
				else if (hint.key.EndsWith("/Dungeon"))
				{
					fullPath += "ScaleFixedCharacters/";
				}
			}
		}
		else if (name.StartsWith("SM_Bld_"))
		{
			fullPath += "Buildings/";
		}
		else if (name.StartsWith("SM_Env_"))
		{
			fullPath += "Environments/";
		}
		else if (name.StartsWith("SM_Prop_"))
		{
			fullPath += "Props/";
		}
		else if (name.StartsWith("FX_"))
		{
			fullPath += "FX/";
		}
		else if (name.StartsWith("Icon_"))
		{
			fullPath += "Icons/";
		}
		else if (name.StartsWith("SM_Veh_"))
		{
			fullPath += "Vehicles/";
		}
		else if (name.StartsWith("SM_Wep_"))
		{
			fullPath += "Weapons/";
		}
		else if (name.StartsWith("SM_Sign_"))
		{
			fullPath += "Signs/";
		}

		return fullPath;
	}

	public List<string> DetermineMaterialPaths(string name, ArtPackHint hint)
	{
		Assert.IsNotNull(name);
		Assert.IsNotNull(hint);

		DebugUtils.Log(DebugContext.Deserialization, $"Determining path options for Synty Studios material {name}...");

		// Default
		List<string> result = new()
		{{
			$"{hint.path}/Materials/"
		}};

		/*
			In case a pack has an additional directory outside of Materials, this approach could be used:

			string pack = hint.key.Split("/")[1];
			switch (pack)
			{
				case "PackName":
					result.Add($"Assets/{hint.path}AlternativeMaterials/");
					break;
			}

		*/

		return result;
	}

}

}