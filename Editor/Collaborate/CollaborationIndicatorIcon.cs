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

using UnityEditor;
using UnityEngine;

namespace Prefabby
{

[InitializeOnLoad]
public class CollaborationIndicatorIcon
{

	private static readonly Texture2D icon;
	private static readonly GUIContent content;
	private static readonly float iconWidth = 15f;
	private static readonly Vector2 iconSize;

	static CollaborationIndicatorIcon()
	{
		icon = Resources.Load("PrefabbyIndicator") as Texture2D;
		if (icon == null)
		{
			return;
		}
		content = new GUIContent(icon);
		iconSize = new Vector2(iconWidth, iconWidth);

		EditorApplication.hierarchyWindowItemOnGUI += DrawIconOnWindowItem;
	}

	private static void DrawIconOnWindowItem(int instanceID, Rect rect)
	{
		GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
		if (go == null)
		{
			return;
		}

		if (!go.TryGetComponent<PrefabbyCollaborationMarker>(out _))
		{
			return;
		}

		EditorGUIUtility.SetIconSize(iconSize);
		var padding = new Vector2(5, 0);
		var iconDrawRect = new Rect(rect.xMax - (iconWidth + padding.x), rect.yMin, rect.width, rect.height);
		EditorGUI.LabelField(iconDrawRect, content);
		EditorGUIUtility.SetIconSize(Vector2.zero);
	}

}

}
