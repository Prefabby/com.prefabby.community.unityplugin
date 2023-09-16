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

class PolyperfectArtPackHelper : IArtPackHelper
{

	public string CreatePrefabHint(GameObject go, SerializedGameObject sgo, string prefabPath, string prefabName, GameObject prefab, PrefabDictionaryItem item)
	{
		string hint = "";

		if (prefabPath.EndsWith("_M"))
		{
			hint += "Material";
		}

		return string.IsNullOrEmpty(hint) ? null : hint;
	}

	public string DeterminePrefabPath(PrefabReference prefab, ArtPackHint hint)
	{
		Assert.IsNotNull(prefab);
		Assert.IsNotNull(hint);

		string name = prefab.name;

		DebugUtils.Log(DebugContext.Deserialization, $"Determining path for polyperfect prefab {name}...");

		string extension = prefab.hint == "Material" ? "M" : "T";
		string fullPath = $"{hint.path}/{extension}/";

		if (prefab.type == "Model")
		{
			fullPath += $"- Meshes_{extension}/";
		}
		else
		{
			fullPath += $"- Prefabs_{extension}/";
		}

		return fullPath;
	}

	public List<string> DetermineMaterialPaths(string name, ArtPackHint hint)
	{
		Assert.IsNotNull(name);
		Assert.IsNotNull(hint);

		DebugUtils.Log(DebugContext.Deserialization, $"Determining path options for polyperfect material {name}...");

		// Default
		List<string> result = new()
		{{
			$"{hint.path}/- Materials/"
		}};

		return result;
	}

}

}
