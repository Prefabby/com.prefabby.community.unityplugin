using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

namespace Prefabby
{

class RequiredArtPacksWindow : EditorWindow
{

	private List<ArtPackHint> requiredArtPacks;
	private ISettingsAccessor settingsAccessor;
	private string message;

	private Vector2 scrollPosition = new();

	public static RequiredArtPacksWindow Show(List<ArtPackHint> missingArtPacks, ISettingsAccessor settingsAccessor, string message)
	{
		RequiredArtPacksWindow window = GetWindow(typeof(RequiredArtPacksWindow), true, "Art Packs missing in project", false) as RequiredArtPacksWindow;
		window.requiredArtPacks = missingArtPacks;
		window.settingsAccessor = settingsAccessor;
		window.message = message;
		return window;
	}

	void OnGUI()
	{
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.windowMargin);

		GUILayout.Label($"{message} Click on \"Details\" for more information about the pack.", GUI.richTextLabelStyle);
		EditorGUILayout.Space();

		foreach (ArtPackHint requiredArtPack in requiredArtPacks)
		{
			EditorGUILayout.BeginHorizontal(GUI.windowMargin);
			GUILayout.Label($"<b>{requiredArtPack.name}</b> by <b>{requiredArtPack.publisherName}</b>", GUI.richTextLabelStyle);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Details"))
			{
				Application.OpenURL($"{Region.DeriveStorefrontApiHost(settingsAccessor.GetApiHost())}/publishers/{requiredArtPack.publisherId}/{requiredArtPack.id}");
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
		}

		GUILayout.FlexibleSpace();

		if (GUILayout.Button("Close", GUILayout.Height(32)))
		{
			Close();
		}

		EditorGUILayout.Space();
		EditorGUILayout.EndScrollView();
	}

}

}
