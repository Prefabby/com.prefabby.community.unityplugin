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

[System.Serializable]
public class PublishingTabContent
{

	private readonly PrefabbyWindow owner;

	private EditorRestApi restApi;

	public GameObject objectToPublish;
	private string objectError;

	public string assetTitle;
	private bool assetTitleError;
	public string assetDescription;

	public PublishingTabContent(PrefabbyWindow owner)
	{
		this.owner = owner;
	}

	private void HandleSettingsChanged()
	{
		restApi = new EditorRestApi(this, owner.Settings);
	}

	public void OnEnable()
	{
		this.owner.OnSettingsChanged += HandleSettingsChanged;

		restApi = new EditorRestApi(this, owner.Settings);

		PrefabbyAssetMarkerEditor.settingsAccessor = owner.Settings;
	}

	public void OnDisable()
	{
		this.owner.OnSettingsChanged -= HandleSettingsChanged;

		PrefabbyAssetMarkerEditor.settingsAccessor = null;
	}

	public void OnGUI()
	{
		// Object publishing

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label("Object To Publish", EditorStyles.boldLabel);
		EditorGUI.BeginChangeCheck();
		objectToPublish = (GameObject) EditorGUILayout.ObjectField(objectToPublish, typeof(GameObject), true);
		if (EditorGUI.EndChangeCheck())
		{
			assetTitle = "";
			assetDescription = "";

			CheckObject();
		}
		if (!string.IsNullOrEmpty(objectError))
		{
			EditorGUILayout.HelpBox(objectError, MessageType.Error);
		}

		EditorGUILayout.Space();

		GUILayout.Label("Title", EditorStyles.boldLabel);
		assetTitle = EditorGUILayout.TextField(assetTitle);
		if (string.IsNullOrEmpty(assetTitle))
		{
			GUI.Placeholder("A short title for display on the browser. Keep it clean, please!");
		}
		if (assetTitleError)
		{
			EditorGUILayout.HelpBox($"You must provide a title with a length of {PublishingConstants.assetTitleMinLength} to {PublishingConstants.assetTitleMaxLength} characters!", MessageType.Error);
		}

		EditorGUILayout.Space();

		GUILayout.Label("Description", EditorStyles.boldLabel);
		assetDescription = EditorGUILayout.TextArea(assetDescription, GUI.textAreaStyle, GUILayout.MinHeight(100));
		if (string.IsNullOrEmpty(assetDescription))
		{
			GUI.Placeholder("Please add some more details that may not be obvious from a single preview image. For example, whether a building comes with interior.", true);
		}

		EditorGUILayout.Space();

		PrefabbyAssetMarker prefabbyAssetMarker = null;
		if (objectToPublish != null)
		{
			prefabbyAssetMarker = objectToPublish.GetComponent<PrefabbyAssetMarker>();
		}
		string buttonLabel = prefabbyAssetMarker == null ? "Publish" : "Update";
		if (GUILayout.Button(buttonLabel, GUILayout.Height(32)))
		{
			StartPublish();
		}

		EditorGUILayout.EndVertical();
	}

	public void SetObjectToPublish(GameObject go)
	{
		objectToPublish = go;
		assetTitle = "";
		assetDescription = "";

		CheckObject();
	}

	private void CheckObject()
	{
		if (objectToPublish == null)
		{
			assetTitle = "";
			assetDescription = "";

			return;
		}

		objectError = null;

		// If this is an already published asset, fetch details
		if (objectToPublish.TryGetComponent<PrefabbyAssetMarker>(out var prefabbyAssetMarker))
		{
			restApi.GetAsset(
				prefabbyAssetMarker.assetId,
				false,
				(AssetInfo assetInfo) => {
					assetTitle = assetInfo.title;
					assetDescription = assetInfo.description;
				},
				() => {
					EditorUtils.GenericError();
				}
			);
		}
	}

	void CheckTitle()
	{
		assetTitleError = assetTitle.Length < PublishingConstants.assetTitleMinLength || assetTitle.Length > PublishingConstants.assetTitleMaxLength;
	}

	bool CheckAll()
	{
		CheckObject();
		CheckTitle();

		return string.IsNullOrEmpty(objectError) && !assetTitleError;
	}

	void StartPublish()
	{
		if (objectToPublish == null)
		{
			objectError = "Please select an object to publish!";
			return;
		}

		if (!CheckAll())
		{
			return;
		}

		PrefabDictionary prefabDictionary = new();
		PrefabIdentification prefabIdentification = new(restApi, null, owner.Settings.accessKey);
		prefabIdentification.IdentifyPrefabs(
			objectToPublish.transform,
			prefabDictionary,
			() => {
				ContinuePublish(prefabDictionary);
			},
			(ErrorInfo errorInfo) => {
				EditorUtils.HandlePrefabIdentificationError(errorInfo);
			}
		);
	}

	private void ContinuePublish(PrefabDictionary prefabDictionary)
	{
		ISerializer serializer = new JsonV1Serializer(objectToPublish, prefabDictionary);
		SerializedTree result = serializer.Serialize();

		// Reset root element details; export should be independent from scene
		SerializedGameObject root = result.FindById(result.root);
		root.siblingIndex = null;
		root.position = null;
		root.rotation = null;
		root.scale = null;

		CreateAssetRequest createAssetRequest = new()
		{
			title = assetTitle,
			description = assetDescription,
			representation = serializer.GetRepresentation(),
			content = result,
			prefabDictionary = prefabDictionary
		};

		if (!objectToPublish.TryGetComponent<PrefabbyAssetMarker>(out var prefabbyAssetMarker))
		{
			DebugUtils.Log(DebugContext.Publishing, "Object is not a Prefabby asset yet, creating new asset...");

			restApi.PublishAsset(
				owner.Settings.accessKey,
				createAssetRequest,
				(AssetInfo assetInfo) => {
					EditorUtils.AttachAssetMarkerToGameObject(objectToPublish, assetInfo);

					EditorUtility.DisplayDialog("Success", "Your asset was successfully uploaded!", "OK");
				},
				() => {
					EditorUtils.GenericError();
				}
			);
		}
		else
		{
			DebugUtils.Log(DebugContext.Publishing, $"Object is already a Prefabby asset with ID {prefabbyAssetMarker.assetId}, updating...");

			restApi.UpdateAsset(
				owner.Settings.accessKey,
				prefabbyAssetMarker.assetId,
				createAssetRequest,
				(AssetInfo assetInfo) => {
					prefabbyAssetMarker.version = assetInfo.version;

					EditorUtility.DisplayDialog("Success", "Your asset was successfully updated!", "OK");
				},
				() => {
					EditorUtils.GenericError();
				}
			);
		}
	}

}

}
