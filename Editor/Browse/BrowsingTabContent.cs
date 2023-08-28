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
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Prefabby
{

[System.Serializable]
public class BrowsingTabContent
{

	private const int itemsPerColumn = 3;

	private readonly PrefabbyWindow owner;

	private EditorRestApi restApi;

	private Vector2 scrollPosition;
	private bool loading = true;
	private string query = "";
	private List<StorefrontAsset> storefrontAssets;
	private Dictionary<string, Texture2D> textures;

	public BrowsingTabContent(PrefabbyWindow owner)
	{
		this.owner = owner;
	}

	private void HandleSettingsChanged()
	{
		restApi = new EditorRestApi(this, owner.Settings.apiHost);
	}

	public void OnEnable()
	{
		owner.OnSettingsChanged += HandleSettingsChanged;

		restApi = new EditorRestApi(this, owner.Settings.apiHost);

		RefreshItems();
	}

	public void OnDisable()
	{
		owner.OnSettingsChanged -= HandleSettingsChanged;
	}

	public void OnGUI(Rect position)
	{
		int safeWidth = (int) position.width - itemsPerColumn * 22;
		int columnWidth = safeWidth / itemsPerColumn;

		GUILayout.BeginHorizontal();
		query = EditorGUILayout.TextField(query);
		if (string.IsNullOrEmpty(query))
		{
			GUI.Placeholder("Search...");
		}
		if (GUILayout.Button("Search", GUILayout.ExpandWidth(false)))
		{
			RefreshItems();
		}
		GUILayout.EndHorizontal();
		EditorGUILayout.Space();

		if (loading)
		{
			GUI.CenteredText("Loading...");
		}
		else
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(3); // TODO This fixes an alignment issue in 2021.3 / macOS, test on other systems
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUIStyle.none, UnityEngine.GUI.skin.verticalScrollbar);
			if (storefrontAssets == null || storefrontAssets.Count == 0)
			{
				EditorGUILayout.HelpBox("No assets found! Start creating one and publish it on Prefabby!", MessageType.Info);
			}
			else
			{
				int numberOfItems = storefrontAssets.Count;
				if (numberOfItems % itemsPerColumn != 0)
				{
					numberOfItems += itemsPerColumn - (numberOfItems % itemsPerColumn);
				}

				for (int i = 0; i < numberOfItems; ++i)
				{
					if (i % itemsPerColumn == 0)
					{
						EditorGUILayout.BeginHorizontal(GUIStyle.none);
					}

					EditorGUILayout.BeginVertical(GUIStyle.none);

					StorefrontAsset asset = i < storefrontAssets.Count ? storefrontAssets[i] : null;
					Texture2D itemTexture = asset != null && asset.previewImageUrl != null && textures.ContainsKey(asset.previewImageUrl) ? textures[asset.previewImageUrl] : Texture2D.blackTexture;

					GUILayout.Box(GUIContent.none, GUIStyle.none, GUILayout.Width(columnWidth), GUILayout.Height(columnWidth));

					if (asset != null)
					{
						Rect pos = new(GUILayoutUtility.GetLastRect());
						if (UnityEngine.GUI.Button(pos, itemTexture) && asset != null)
						{
							ImportPreviewWindow.Show(
								asset,
								asset.previewImageUrl != null && textures.ContainsKey(asset.previewImageUrl) ? textures[asset.previewImageUrl] : null,
								owner.Settings,
								ImportAsset
							);
						}
						if (columnWidth > 150)
						{
							EditorGUILayout.BeginVertical(GUILayout.MaxWidth(columnWidth));
							GUILayout.Label(asset.title, GUI.previewTitleStyle);
							GUILayout.Label($"by {asset.creatorDisplayName}", GUI.previewCreatorStyle);
							EditorGUILayout.EndVertical();
						}
						else
						{
							EditorGUILayout.Space();
						}
					}

					EditorGUILayout.EndVertical();

					if (i % itemsPerColumn == itemsPerColumn - 1)
					{
						EditorGUILayout.EndHorizontal();
					}
				}
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndHorizontal();
		}
	}

	private void ImportAsset(AssetInfo assetInfo)
	{
		Assert.IsNotNull(assetInfo);

		// Ensure all required art packs are installed
		List<ArtPackHint> availableArtPacks = EditorUtils.GetAvailableArtPacks(DebugContext.Browsing, assetInfo.requiredArtPacks);
		if (availableArtPacks.Count != assetInfo.requiredArtPacks.Count)
		{
			List<ArtPackHint> missingArtPacks = assetInfo.requiredArtPacks.Except(availableArtPacks).ToList();
			RequiredArtPacksWindow.Show(missingArtPacks, owner.Settings, "The following packs are required for this asset but seem to be missing in the project.");
			return;
		}

		// Determine parent
		Transform parent = null;
		if (Selection.transforms.Length == 1)
		{
			parent = Selection.transforms[0];
		}

		Transform result = null;

		switch (assetInfo?.representation)
		{
			case "JsonV1":
				result = new JsonV1Deserializer(new EditorInstanceCreator(), new EditorSerializationErrorHandler(), assetInfo.content, assetInfo.prefabDictionary)
						.Deserialize(parent);
				break;
			default:
				EditorUtils.Error("Unknown asset representation: {assetInfo?.representation}. Try upgrading the plugin.");
				break;
		}

		// Focus created object
		if (result != null)
		{
			EditorUtils.AttachAssetMarkerToGameObject(result.gameObject, assetInfo);

			Selection.activeGameObject = result.gameObject;
			SceneView.FrameLastActiveSceneView();
		}
	}

	private void RefreshItems()
	{
		loading = true;
		owner.Repaint();

		restApi.GetStorefrontAssets(
			query,
			(GetStorefrontAssetsResponse response) => {
				storefrontAssets = response.data;
				loading = false;
				owner.Repaint();

				// Load textures asynchronously
				textures = new Dictionary<string, Texture2D>();
				foreach (StorefrontAsset asset in storefrontAssets)
				{
					if (asset.previewImageUrl != null)
					{
						restApi.GetImage(
							asset.previewImageUrl,
							(Texture2D texture) => {
								textures.Add(asset.previewImageUrl, texture);
								owner.Repaint();
							},
							() => {
								EditorUtils.GenericError();
							}
						);
					}
				}
			},
			() => {
				EditorUtils.GenericError();
			}
		);
	}

}

}
