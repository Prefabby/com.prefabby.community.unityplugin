/*
	Prefabby Unity plugin
    Copyright (C) 2023-2024  Matthias Gall <matt@prefabby.com>

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

using System;

using UnityEngine;
using UnityEditor;

namespace Prefabby
{

class EditorInstanceCreator : IInstanceCreator
{

	public GameObject InstantiateGameObjectFromPath(string path, string name, string type)
	{
		string unityType = MapTypeToFilter(type);
		string extension = MapTypeToExtension(type);

		DebugUtils.Log(DebugContext.Deserialization, $"Trying to find: {path} {name} t:{unityType}");
		string[] guids = AssetDatabase.FindAssets($"{name} t:{unityType}");
		DebugUtils.Log(DebugContext.Deserialization, $"Found {guids.Length} candidates: {guids}");

		for (int i = 0; i < guids.Length; ++i)
		{
			string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
			DebugUtils.Log(DebugContext.Deserialization, $"Checking if prefab path {prefabPath} contains path {path} and ends with /{name}.{extension}...");

			// Make sure the found prefab has the requested name/extension,
			// see material method below for a more specific example
			if (prefabPath.Contains(path, StringComparison.OrdinalIgnoreCase) && prefabPath.EndsWith($"/{name}.{extension}", StringComparison.OrdinalIgnoreCase))
			{
				DebugUtils.Log(DebugContext.Deserialization, $"Found {unityType} {name} at: {prefabPath}");

				GameObject prefab = (GameObject) AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject));
				PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(prefab);

				if (type == null ||
					(type == "Prefab" && prefabAssetType == PrefabAssetType.Regular) ||
					(type == "Model" && prefabAssetType == PrefabAssetType.Model) ||
					(type == "Variant" && prefabAssetType == PrefabAssetType.Variant))
				{
					return (GameObject) PrefabUtility.InstantiatePrefab(prefab);
				}
			}
		}
		return null;
	}

	public Material InstantiateMaterialFromPath(string path, string name)
	{
		DebugUtils.Log(DebugContext.Deserialization, $"Trying to find: {name} t:Material");
		string[] guids = AssetDatabase.FindAssets($"{name} t:Material");

		for (int i = 0; i < guids.Length; ++i)
		{
			string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);

			// When searching for "Wall_01_A", this method may find e.g. "Wall_01_Alt_01_Triplanar", too.
			// Therefore we're making sure the file ends with the requested name.
			// To avoid that phone.prefab finds gramophone.prefab, too, we also include the leading slash.
			if (prefabPath.Contains(path, StringComparison.OrdinalIgnoreCase) && prefabPath.EndsWith($"/{name}.mat", StringComparison.OrdinalIgnoreCase))
			{
				DebugUtils.Log(DebugContext.Deserialization, $"Found material {name} at: {prefabPath}");
				return (Material) AssetDatabase.LoadAssetAtPath(prefabPath, typeof(Material));
			}
		}
		return null;
	}

	private string MapTypeToFilter(string type)
	{
		return type switch
		{
			null => "prefab",
			"Model" => "model",
			"Prefab" or "Variant" => "prefab",
			_ => "",
		};
	}

	private string MapTypeToExtension(string type)
	{
		return type switch
		{
			null => "prefab",
			"Model" => "fbx",
			"Prefab" or "Variant" => "prefab",
			_ => "",
		};
	}

}

}
