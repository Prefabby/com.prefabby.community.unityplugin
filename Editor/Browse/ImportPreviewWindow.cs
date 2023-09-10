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
	private AssetInfo assetInfo;
	private List<ArtPackHint> missingArtPacks;
	private int numberOfPrefabs = -1;
	private Texture2D texture;
	private ISettingsAccessor settingsAccessor;
	private Action<AssetInfo> importCallback;

	private Vector2 scrollPosition = new();
	private Vector2 descriptionScrollPos = new();
	private Vector2 requiredArtPacksScrollPos = new();

	public static ImportPreviewWindow Show(StorefrontAsset storefrontAsset, Texture2D texture, ISettingsAccessor settingsAccessor, Action<AssetInfo> importCallback)
	{
		ImportPreviewWindow window = GetWindow(typeof(ImportPreviewWindow), true, "Import Asset", false) as ImportPreviewWindow;
		window.storefrontAsset = storefrontAsset;
		window.assetInfo = null;
		window.texture = texture;
		window.settingsAccessor = settingsAccessor;
		window.importCallback = importCallback;
		window.LoadAsset();

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

	public void LoadAsset()
	{
		EditorRestApi restApi = new(this, settingsAccessor);
		restApi.GetAsset(
			storefrontAsset.id,
			false,
			(AssetInfo assetInfo) => {
				List<ArtPackHint> availableArtPacks = EditorUtils.GetAvailableArtPacks(DebugContext.Browsing, assetInfo.requiredArtPacks);
				missingArtPacks = assetInfo.requiredArtPacks.Except(availableArtPacks).ToList();
				numberOfPrefabs = CountNodesWithPrefabs(assetInfo.content);
				this.assetInfo = assetInfo;
				Repaint();
			},
			() => {
				EditorUtils.GenericError();
			}
		);
	}

	private int CountNodesWithPrefabs(SerializedTree tree)
	{
		int count = 0;
		foreach (SerializedGameObject serializedGameObject in tree.gameObjects)
		{
			if (serializedGameObject.prefab != null)
			{
				count++;
			}
		}
		return count;
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

		if (assetInfo == null)
		{
			GUI.CenteredText("Loading details...");
		}
		else
		{
			GUILayout.Label("Description", EditorStyles.boldLabel);
			descriptionScrollPos = GUILayout.BeginScrollView(descriptionScrollPos, EditorStyles.textArea, GUILayout.Height(100));
			GUILayout.Label(assetInfo.description, EditorStyles.wordWrappedLabel);
			GUILayout.EndScrollView();

			EditorGUILayout.Space();

			GUILayout.Label("Required Art Packs", EditorStyles.boldLabel);
			requiredArtPacksScrollPos = GUILayout.BeginScrollView(requiredArtPacksScrollPos, EditorStyles.textArea, GUILayout.Height(100));
			foreach (ArtPackHint artPack in assetInfo.requiredArtPacks)
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
			GUILayout.Label($"{numberOfPrefabs}");

			EditorGUILayout.Space();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Import", GUILayout.Height(32)))
			{
				importCallback(assetInfo);
				Close();
			}
			EditorGUILayout.Space();
			if (GUILayout.Button("Close", GUILayout.Height(32)))
			{
				Close();
			}
		}

		// End details
		EditorGUILayout.EndVertical();

		// End columns
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.EndScrollView();
	}

}

}
