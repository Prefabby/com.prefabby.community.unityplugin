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

[Serializable]
public class SettingsTabContent
{

	private const string settingsAsset = "Assets/PrefabbySettings.asset";

	[SerializeField]
	private Settings settings;

	private readonly PrefabbyWindow owner;

	private int regionForEdit;
	private string regionError;
	private string accessKeyForEdit;
	private string accessKeyError;
	private bool showCollaborationLimitationsNoticeForEdit;
	private float hierarchyCheckDeltaTimeForEdit;
	private string hierarchyCheckDeltaTimeError;
	private bool showCollaboratorSelectionForEdit;
	private bool showActivityLogForEdit;
	private bool stopOnInvalidGameObjectsForEdit;
	private bool createBackupsForEdit;
	private bool forceRefreshForEdit;
	private bool loggingForEdit;

	private string[] regionOptions;

	internal Settings Settings
	{
		get
		{
			return settings;
		}
	}

	public SettingsTabContent(PrefabbyWindow owner)
	{
		this.owner = owner;
	}

	public void OnEnable()
	{
		if (settings == null)
		{
			settings = AssetDatabase.LoadAssetAtPath<Settings>(settingsAsset);

			if (settings == null)
			{
				DebugUtils.Log(DebugContext.Settings, "No Prefabby settings exist yet, creating file");

				settings = ScriptableObject.CreateInstance<Settings>();
				AssetDatabase.CreateAsset(settings, settingsAsset);
			}
		}

		regionOptions = Region.DeriveRegionOptions();
		regionError = "";
		accessKeyError = "";
		hierarchyCheckDeltaTimeError = "";

		LoadCurrentSettings();
	}

	public void OnDisable()
	{
	}

	public void OnGUI()
	{
		// Settings data

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label("Settings Data", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		EditorGUI.BeginChangeCheck();
		settings = EditorGUILayout.ObjectField(settings, typeof(Settings), false) as Settings;
		if (EditorGUI.EndChangeCheck())
		{
			LoadCurrentSettings();
			owner.TriggerSettingsChanged();
		}
		EditorGUILayout.Space();

		EditorGUILayout.EndVertical();

		EditorGUILayout.Space();

		// General settings

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label("General Settings", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		GUILayout.Label("Region:", EditorStyles.wordWrappedLabel);
		regionForEdit = EditorGUILayout.Popup(regionForEdit, regionOptions);
		if (!string.IsNullOrEmpty(regionError))
		{
			EditorGUILayout.HelpBox(regionError, MessageType.Error);
		}
		EditorGUILayout.Space();

		GUILayout.Label("Access Key:", EditorStyles.wordWrappedLabel);
		accessKeyForEdit = EditorGUILayout.TextField(accessKeyForEdit);
		if (!string.IsNullOrEmpty(accessKeyError))
		{
			EditorGUILayout.HelpBox(accessKeyError, MessageType.Error);
		}
		EditorGUILayout.Space();

		if (regionForEdit > 0)
		{
			string apiHost = Region.DeriveApiHost(regionForEdit);
			GUILayout.Label("No Access Key yet? Get one on the respective Prefabby homepage by clicking below:", EditorStyles.wordWrappedLabel);
			if (GUILayout.Button($"Go to {apiHost}"))
			{
				Application.OpenURL(apiHost);
			}
		}

		EditorGUILayout.EndVertical();

		EditorGUILayout.Space();

		// Collaboration settings

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label("Collaboration Settings", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		showCollaborationLimitationsNoticeForEdit = EditorGUILayout.Toggle("", showCollaborationLimitationsNoticeForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Show limitations notice when starting/joining collaboration");
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();

		EditorGUILayout.LabelField("Time between hierarchy checks (s):", EditorStyles.wordWrappedLabel);
		hierarchyCheckDeltaTimeForEdit = EditorGUILayout.FloatField(hierarchyCheckDeltaTimeForEdit);
		if (!string.IsNullOrEmpty(hierarchyCheckDeltaTimeError))
		{
			EditorGUILayout.HelpBox(hierarchyCheckDeltaTimeError, MessageType.Error);
		}
		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		showCollaboratorSelectionForEdit = EditorGUILayout.Toggle("", showCollaboratorSelectionForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Show collaborator selection");
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		/*

		// The functionality for these two settings still needs to be implemented:

		EditorGUILayout.BeginHorizontal();
		stopOnInvalidGameObjectsForEdit = EditorGUILayout.Toggle("", stopOnInvalidGameObjectsForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Stop synchronisation if invalid game objects with unknown components are found", EditorStyles.wordWrappedLabel);
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		createBackupsForEdit = EditorGUILayout.Toggle("", createBackupsForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Create backup objects");
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		*/

		EditorGUILayout.BeginHorizontal();
		showActivityLogForEdit = EditorGUILayout.Toggle("", showActivityLogForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Show activity log");
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		loggingForEdit = EditorGUILayout.Toggle("", loggingForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("Logging (verbose!)");
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		forceRefreshForEdit = EditorGUILayout.Toggle("", forceRefreshForEdit, GUILayout.Width(20));
		EditorGUILayout.LabelField("DEBUG: Force refresh after message handling");
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.EndVertical();

		EditorGUILayout.Space();

		// No publishing settings so far

		// Save settings

		if (GUILayout.Button("Save", GUILayout.Height(32)))
		{
			SaveChangedSettings();
		}
	}

	private void LoadCurrentSettings()
	{
		Settings initializer = settings ?? ScriptableObject.CreateInstance<Settings>();

		regionForEdit = Region.DeriveRegion(initializer.apiHost);
		showCollaborationLimitationsNoticeForEdit = initializer.showCollaborationLimitationsNotice;
		hierarchyCheckDeltaTimeForEdit = initializer.hierachyCheckDeltaTime;
		showCollaboratorSelectionForEdit = initializer.showCollaboratorSelection;
		showActivityLogForEdit = initializer.showActivityLog;
		accessKeyForEdit = initializer.accessKey;
		createBackupsForEdit = initializer.createBackups;
		forceRefreshForEdit = initializer.forceRefresh;
		loggingForEdit = initializer.logging;

		DebugUtils.logEnabled = initializer.logging;
	}

	private void SaveChangedSettings()
	{
		DebugUtils.Log(DebugContext.Settings, "Verifying provided access key...");

		if (regionForEdit == 0)
		{
			regionError = "Please select a region!";
			return;
		}
		else
		{
			regionError = "";
		}
		if (string.IsNullOrEmpty(accessKeyForEdit))
		{
			accessKeyError = "Please enter an access key!";
			return;
		}
		else
		{
			accessKeyError = "";
		}
		if (hierarchyCheckDeltaTimeForEdit < 0 || hierarchyCheckDeltaTimeForEdit > 10)
		{
			hierarchyCheckDeltaTimeError = "Please use a value between 0 and 10 seconds";
		}

		bool changedAccessKey = settings.accessKey != accessKeyForEdit;
		string derivedApiHost = Region.DeriveApiHost(regionForEdit);
		// Test new settings
		TempSettingsAccessor tempSettingsAccessor = new TempSettingsAccessor(derivedApiHost);
		EditorRestApi restApi = new(this, tempSettingsAccessor);
		restApi.GetUserDetails(
			accessKeyForEdit,
			(userInfo) => {
				DebugUtils.Log(DebugContext.Settings, $"Verified access key, user is {userInfo.displayName} (ID: {userInfo.id})");

				settings.apiHost = derivedApiHost;
				settings.accessKey = accessKeyForEdit;
				settings.userId = userInfo.id;
				settings.showCollaborationLimitationsNotice = showCollaborationLimitationsNoticeForEdit;
				settings.hierachyCheckDeltaTime = hierarchyCheckDeltaTimeForEdit;
				settings.showCollaboratorSelection = showCollaboratorSelectionForEdit;
				settings.showActivityLog = showActivityLogForEdit;
				settings.createBackups = createBackupsForEdit;
				settings.forceRefresh = forceRefreshForEdit;
				settings.logging = loggingForEdit;

				DebugUtils.logEnabled = settings.logging;

				EditorUtility.SetDirty(settings);

				if (changedAccessKey)
				{
					EditorUtility.DisplayDialog("Success", $"Hello {userInfo.displayName}, your access key has been validated. You're good to go!", "OK");
				}

				owner.TriggerSettingsChanged();
			},
			() => {
				accessKeyError = "Failed to fetch user details; does the access key fit to the region?";
				EditorUtility.DisplayDialog("Error", "Failed to fetch user details, invalid access key? Changes are not saved.", "OK");
				DebugUtils.Log(DebugContext.Settings, "Failed to fetch user details, invalid access key? Changes are not saved.");
			}
		);
	}

}

}
