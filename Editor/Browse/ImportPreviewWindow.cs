using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

namespace Prefabby
{

class ImportPreviewWindow : EditorWindow
{

	private const int previewPadding = 4;
	private const int spacer = 10;

	private GUIStyle iconStyle;
	private Texture2D checkmark;
	private Texture2D cross;

	private StorefrontAsset storefrontAsset;
	private List<ArtPackHint> missingArtPacks;
	private Texture2D texture;
	private ISettingsAccessor settingsAccessor;
	private Action<AssetInfo> importCallback;
	private bool loading = false;

	private Vector2 scrollPosition = new();
	private Vector2 descriptionScrollPos = new();
	private Vector2 requiredArtPacksScrollPos = new();

	public static ImportPreviewWindow Show(StorefrontAsset storefrontAsset, Texture2D texture, ISettingsAccessor settingsAccessor, Action<AssetInfo> importCallback)
	{
		ImportPreviewWindow window = GetWindow(typeof(ImportPreviewWindow), true, "Import Creation", false) as ImportPreviewWindow;
		window.storefrontAsset = storefrontAsset;
		window.texture = texture;
		window.settingsAccessor = settingsAccessor;
		window.importCallback = importCallback;
		window.CheckRequiredArtPacks();

		EditorUtils.CenterEditorWindow(window, 1000, 500);

		return window;
	}

	void OnEnable()
	{
		iconStyle = new GUIStyle()
		{
			padding = new RectOffset(2, 2, 2, 2)
		};

		checkmark = Resources.Load("icons8-checkmark-16") as Texture2D;
		cross = Resources.Load("icons8-x-16") as Texture2D;
	}

	public void CheckRequiredArtPacks()
	{
		List<ArtPackHint> availableArtPacks = EditorUtils.GetAvailableArtPacks(DebugContext.Browsing, storefrontAsset.requiredArtPacks);
		missingArtPacks = storefrontAsset.requiredArtPacks.Except(availableArtPacks).ToList();
	}

	void OnGUI()
	{
		int columnWidth = (int)(position.width / 2 - spacer);

		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.windowMargin);

		// Three columns
		EditorGUILayout.BeginHorizontal();

		// Preview image
		GUILayout.Box(null as Texture2D, GUI.previewImageContainerStyle, GUILayout.Width(columnWidth), GUILayout.Height(columnWidth), GUILayout.ExpandHeight(false));
		Rect pos = new(GUILayoutUtility.GetLastRect());
		pos.x += previewPadding;
		pos.y += previewPadding;
		pos.width -= previewPadding * 2;
		pos.height -= previewPadding * 2;
		UnityEngine.GUI.DrawTexture(pos, texture, ScaleMode.StretchToFill);

		// Spacer
		EditorGUILayout.Space(spacer, false);

		// Details
		EditorGUILayout.BeginVertical();
		GUILayout.Label(storefrontAsset.title.Trim(), GUI.previewBigTitleStyle);
		GUILayout.Label($"by {storefrontAsset.creatorDisplayName}", GUI.previewCreatorStyle);
		EditorGUILayout.LabelField("", UnityEngine.GUI.skin.horizontalSlider);

		GUILayout.Label("Description", EditorStyles.boldLabel);
		descriptionScrollPos = GUILayout.BeginScrollView(descriptionScrollPos, EditorStyles.textArea, GUILayout.Height(100));
		GUILayout.Label(storefrontAsset.description, EditorStyles.wordWrappedLabel);
		GUILayout.EndScrollView();

		EditorGUILayout.Space();

		GUILayout.Label("Required Art Packs", EditorStyles.boldLabel);
		requiredArtPacksScrollPos = GUILayout.BeginScrollView(requiredArtPacksScrollPos, EditorStyles.textArea, GUILayout.Height(100));
		foreach (ArtPackHint artPack in storefrontAsset.requiredArtPacks)
		{
			bool isMissing = missingArtPacks.Contains(artPack);
			EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
			GUILayout.Box(isMissing ? cross : checkmark, iconStyle, GUILayout.Width(20), GUILayout.Height(20));
			GUILayout.Label($"{artPack.name} by {artPack.publisherName}", EditorStyles.wordWrappedLabel);
			EditorGUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();

		EditorGUILayout.Space();

		GUILayout.Label("Number of nodes with prefabs", EditorStyles.boldLabel);
		GUILayout.Label($"{storefrontAsset.numberOfPrefabs}");

		EditorGUILayout.Space();

		GUILayout.FlexibleSpace();

		UnityEngine.GUI.enabled = !loading;
		if (GUILayout.Button(loading ? "Please wait..." : "Import", GUILayout.Height(32)))
		{
			Import();
		}
		EditorGUILayout.Space();
		UnityEngine.GUI.enabled = true;

		if (GUILayout.Button("Close", GUILayout.Height(32)))
		{
			Close();
		}

		// End details
		EditorGUILayout.EndVertical();

		// End columns
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.EndScrollView();
	}

	private void Import()
	{
		loading = true;
		EditorRestApi restApi = new(this, settingsAccessor);
		restApi.GetAsset(
			storefrontAsset.id,
			true,
			(AssetInfo assetInfo) => {
				loading = false;
				importCallback(assetInfo);
				Close();
			},
			() => {
				loading = false;
				EditorUtils.GenericError();
			}
		);
	}

}

}
