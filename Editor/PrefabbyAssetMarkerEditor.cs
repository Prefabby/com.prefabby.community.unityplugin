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

using UnityEngine;
using UnityEditor;

namespace Prefabby
{

[CustomEditor(typeof(PrefabbyAssetMarker))]
public class PrefabbyAssetMarkerEditor : Editor
{

	public static ISettingsAccessor settingsAccessor;

	public override void OnInspectorGUI()
	{
		PrefabbyAssetMarker marker = (PrefabbyAssetMarker) target;

		EditorGUILayout.HelpBox("This object is a managed Prefabby asset. If you want to publish updates to the Prefabby service or receive updates from the creator, do not remove this component.", MessageType.Info);
		EditorGUILayout.Space();
		EditorGUILayout.LabelField($"Version: {marker.version}");
		EditorGUILayout.Space();

		if (settingsAccessor != null && settingsAccessor.GetUserId() == marker.creatorId)
		{
			EditorGUILayout.LabelField("This is your asset. You can publish updates in the Prefabby Publish window.");

			if (GUILayout.Button("Open Prefabby Publish", GUILayout.Height(32)))
			{
				PrefabbyWindow window = PrefabbyWindow.Init();
				window.SetObjectToPublish(marker.gameObject);
			}
		}

		EditorGUILayout.Space();

		if (GUILayout.Button($"Open asset on {Constants.homepage}", GUILayout.Height(32)))
		{
			Application.OpenURL($"{Constants.homepage}/creations/{marker.assetId}?ref=unity-plugin");
		}
	}

}

}
