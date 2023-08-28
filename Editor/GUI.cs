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

using System;

using UnityEngine;
using UnityEditor;

namespace Prefabby
{

class GUI
{
	public static bool initialized = false;

	public static void InitializeIfNecessary()
	{
		if (initialized)
		{
			return;
		}

		DebugUtils.Log(DebugContext.General, "Initializing GUI...");

		windowMargin = new GUIStyle
		{
			margin = new RectOffset(2, 2, 2, 2)
		};
		previewTitleStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
		{
			margin = new RectOffset(0, 0, 4, 0),
			wordWrap = true
		};
		previewImageContainerStyle = new GUIStyle(UnityEngine.GUI.skin.GetStyle("HelpBox"));
		previewCreatorStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
		{
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 10),
			wordWrap = true
		};
		previewBigTitleStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
		{
			fontSize = 16,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 4),
			wordWrap = true
		};
		flatButtonStyle = new GUIStyle
		{
			margin = new RectOffset(4, 4, 4, 4),
			padding = new RectOffset(2, 2, 2, 2),
			alignment = TextAnchor.MiddleLeft,
			normal =
			{
				textColor = UnityEngine.GUI.skin.textField.normal.textColor,
				background = null
			},
		};
		flatButtonStyleSelected = new GUIStyle
		{
			margin = new RectOffset(4, 4, 4, 4),
			padding = new RectOffset(2, 2, 2, 2),
			alignment = TextAnchor.MiddleLeft,
			normal =
			{
				textColor = UnityEngine.GUI.skin.textField.normal.textColor,
				background = CreateColorTexture(new Color(.17f, .36f, .52f, 1f))
			},
		};
		selectableLabelStyle = new GUIStyle
		{
			alignment = TextAnchor.UpperLeft,
			padding = new RectOffset(3, 15, 2, 0),
			wordWrap = true,
			clipping = TextClipping.Clip,
			normal =
			{
				textColor = Color.grey
			},
		};
		placeholderStyle = new GUIStyle
		{
			alignment = TextAnchor.UpperLeft,
			padding = new RectOffset(3, 0, 2, 0),
			fontStyle = FontStyle.Italic,
			wordWrap = false,
			clipping = TextClipping.Clip,
			normal =
			{
				textColor = Color.grey
			},
		};
		placeholderWordwrappedStyle = new GUIStyle
		{
			alignment = TextAnchor.UpperLeft,
			padding = new RectOffset(3, 0, 2, 0),
			fontStyle = FontStyle.Italic,
			wordWrap = true,
			clipping = TextClipping.Clip,
			normal =
			{
				textColor = Color.grey
			},
		};

		groupStyle = new GUIStyle(UnityEngine.GUI.skin.GetStyle("HelpBox"))
		{
			padding = new RectOffset(10, 10, 10, 10)
		};

		titleBarStyle = new GUIStyle(UnityEngine.GUI.skin.GetStyle("HelpBox"))
		{
			padding = new RectOffset(5, 5, 5, 5),
			richText = true
		};
		titleBarStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);

		richTextLabelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
		{
			wordWrap = true,
			richText = true
		};

		// For whatever reason, I have to manually set wordWrap for a textarea control
		textAreaStyle = new GUIStyle(EditorStyles.textArea)
		{
			wordWrap = true
		};

		initialized = true;
	}

	public static GUIStyle windowMargin;
	public static GUIStyle previewTitleStyle;
	public static GUIStyle previewCreatorStyle;
	public static GUIStyle previewImageContainerStyle;
	public static GUIStyle previewBigTitleStyle;
	public static GUIStyle flatButtonStyle;
	public static GUIStyle flatButtonStyleSelected;
	public static GUIStyle selectableLabelStyle;
	public static GUIStyle placeholderStyle;
	public static GUIStyle placeholderWordwrappedStyle;
	public static GUIStyle groupStyle;
	public static GUIStyle richTextLabelStyle;
	public static GUIStyle titleBarStyle;
	public static GUIStyle textAreaStyle;

	public static void CenteredText(string text)
	{
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label(text);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.FlexibleSpace();
	}

	public static void Placeholder(string text, bool wordwrap = false)
	{
		Rect pos = new Rect(GUILayoutUtility.GetLastRect());
		EditorGUI.LabelField(pos, text, wordwrap ? placeholderWordwrappedStyle : placeholderStyle);
	}

	public static Vector3 ScrollableSelectableLabel(Vector3 position, string text, GUIStyle style)
	{
		// Extract scroll position and width from position vector.
		Vector2 scrollPos = new(position.x, position.y);
		float width = position.z;
		scrollPos = GUILayout.BeginScrollView(scrollPos, style, GUILayout.Height(200));
		float pixelHeight = style.CalcHeight(new GUIContent(text), width);
		EditorGUILayout.SelectableLabel(text, selectableLabelStyle, GUILayout.MinHeight(Math.Max(200, pixelHeight)));
		// Update the width on repaint, based on width of the SelectableLabel's rectangle.
		if (Event.current.type == EventType.Repaint)
		{
			width = GUILayoutUtility.GetLastRect().width;
		}
		GUILayout.EndScrollView();
		return new Vector3(scrollPos.x, scrollPos.y, width);
	}

	public static Texture2D CreateColorTexture(Color col)
	{
		Color32[] pixels = new Color32[4];
		Array.Fill(pixels, col);
		Texture2D result = new(2, 2);
		result.SetPixels32(pixels);
		result.Apply();
		return result;
	}

}

}
