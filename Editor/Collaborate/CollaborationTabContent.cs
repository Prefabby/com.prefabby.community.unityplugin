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

using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Prefabby
{

[System.Serializable]
public class CollaborationTabContent
{

	private const string defaultPrefabbyRootGameObjectName = "Prefabby Collaboration";
	private readonly JsonConverter[] debugJsonConverters = new JsonConverter[]{ new Vector3JsonConverter(), new QuaternionJsonConverter() };

	private readonly PrefabbyCommunityWindow owner;

	[SerializeField]
	private CollaborationState collaborationState = null;
	private CollaborationController collaborationController = null;

	private string collaborationIdToJoin = "";
	private string joinCollaborationError = null;
	private Vector3 scrollPos = new();

	private Logger logger;
	private EditorRestApi restApi;
	private StompApi stompApi;

	private Vector2 participantsScrollPos = new();
	private int selectedParticipant = -1;

	private Vector2 previousCollaborationsScrollPos = new();
	private List<CollaborationShortInfo> previousCollaborations;
	private int selectedPreviousCollaboration = -1;

	public CollaborationTabContent(PrefabbyCommunityWindow owner)
	{
		this.owner = owner;
	}

	private void LoadCollaborations()
	{
		if (owner.Settings.IsValid())
		{
			restApi.ListCollaborations(
				owner.Settings.accessKey,
				(List<CollaborationShortInfo> collaborationShortInfoList) => {
					DebugUtils.Log(DebugContext.Collaboration, $"Loaded {collaborationShortInfoList.Count} previous collaborations");
					previousCollaborations = collaborationShortInfoList;
					selectedPreviousCollaboration = -1;
				},
				() => {
					DebugUtils.LogError(DebugContext.Collaboration, "Failed to load previous collaborations");
				}
			);
		}
	}

	private void HandleSettingsChanged()
	{
		DebugUtils.Log(DebugContext.General, "Settings changed");

		restApi = new EditorRestApi(this, owner.Settings);

		LoadCollaborations();
	}

	public void OnEnable()
	{
		owner.OnSettingsChanged += HandleSettingsChanged;

		restApi = new EditorRestApi(this, owner.Settings);

		if (collaborationState != null && collaborationState.collaborationObject != null)
		{
			InitializeForCollaboration();
		}
		else
		{
			PrefabbyCollaborationMarker[] markers = Object.FindObjectsOfType<PrefabbyCollaborationMarker>();
			if (markers.Length > 1)
			{
				EditorUtility.DisplayDialog(
					"Multiple Prefabby Collaborations",
					$"There are {markers.Length} GameObjects with a PrefabbyCollaborationMarker component. You can continue any collaboration session from the Prefabby window.",
					"Ok"
				);
			}
			else if (markers.Length == 1 && owner.Settings.IsValid())
			{
				if (EditorUtility.DisplayDialog(
					"Continue Prefabby Collaboration?",
					"There is one GameObject with a PrefabbyCollaborationMarker component, which indicates a collaboration session. Do you want to continue this session?",
					"Yes, continue", "No, ignore")
				)
				{
					restApi.GetCollaboration(
						owner.Settings.accessKey,
						markers[0].collaborationId,
						(CollaborationInfo collaborationInfo) => {
							DebugUtils.Log(DebugContext.Collaboration, $"Collaboration information received, ID: {collaborationInfo.id}");

							collaborationState = ScriptableObject.CreateInstance<CollaborationState>();
							collaborationState.collaborationId = collaborationInfo.id;
							collaborationState.collaborationOwnerId = collaborationInfo.ownerId;
							collaborationState.participants = collaborationInfo.participants;
							collaborationState.prefabDictionary = collaborationInfo.prefabDictionary;
							collaborationState.collaborationObject = markers[0].gameObject;
							collaborationState.pendingSelectionChanges = new();
							collaborationState.tree = collaborationInfo.content;

							InitializeForCollaboration();
						},
						() => {
							DebugUtils.Log(DebugContext.Collaboration, $"Failed to load collaboration information for collaboration ID {markers[0].collaborationId}");
							EditorUtils.GenericError();
						}
					);
				}
			}
			else
			{
				collaborationState = null;
			}
		}

		LoadCollaborations();
	}

	public void OnDisable()
	{
		owner.OnSettingsChanged -= HandleSettingsChanged;

		if (logger != null)
		{
			logger.OnLogChanged -= HandleLogChanged;
		}
		if (collaborationState != null && collaborationController != null)
		{
			collaborationController.Uninitialize();
			collaborationController = null;
		}
	}

	public void OnSelectionChange()
	{
		if (collaborationState == null)
		{
			owner.Repaint();
		}
	}

	public void OnGUI()
	{
		if (collaborationState == null)
		{
			RenderCollaborationStartGUI();
		}
		else
		{
			RenderCollaborationGUI();
		}
	}

	private void RenderCollaborationStartGUI()
	{
		bool isEnabled = owner.Settings.IsValid();

		if (!isEnabled)
		{
			EditorGUILayout.HelpBox("Please go to the settings to configure your API access first.", MessageType.Error);
			EditorGUILayout.Space();
		}

		UnityEngine.GUI.enabled = isEnabled;

		// Check if the selected transform is a collaboration
		if (Selection.transforms.Length == 1)
		{
			if (Selection.transforms[0].TryGetComponent<PrefabbyCollaborationMarker>(out var collaborationMarker))
			{
				EditorGUILayout.BeginVertical(GUI.groupStyle);

				GUILayout.Label(
					"You selected a GameObject which has a collaboration marker attached. If you wish to rejoin this collaboration, click below. Please note that the object will be resynchronized and may change.",
					EditorStyles.wordWrappedLabel
				);
				EditorGUILayout.Space();

				if (GUILayout.Button("Rejoin collaboration", GUILayout.Height(32)))
				{
					JoinCollaboration(null, collaborationMarker.transform, collaborationMarker.collaborationId);
				}

				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
			}
		}

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label(
			"If you want to collaborate with others, you can start a collaboration session and invite them using the collaboration ID displayed in the next step.",
			EditorStyles.wordWrappedLabel
		);
		EditorGUILayout.Space();

		if (GUILayout.Button("Start a new collaboration", GUILayout.Height(32)))
		{
			StartCollaboration(Selection.transforms.Length == 0 ? null : Selection.transforms[0]);
		}

		EditorGUILayout.EndVertical();

		EditorGUILayout.Space();

		EditorGUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label(
			"If you want to join a running collaboration, you can do so by entering the collaboration ID below.",
			EditorStyles.wordWrappedLabel
		);
		EditorGUILayout.Space();

		collaborationIdToJoin = EditorGUILayout.TextField(collaborationIdToJoin);
		if (!string.IsNullOrEmpty(joinCollaborationError))
		{
			EditorGUILayout.HelpBox(joinCollaborationError, MessageType.Error);
		}
		EditorGUILayout.Space();

		if (GUILayout.Button("Join existing collaboration", GUILayout.Height(32)))
		{
			if (string.IsNullOrEmpty(collaborationIdToJoin))
			{
				joinCollaborationError = "Please enter a collaboration ID.";
			}
			else
			{
				JoinCollaboration(Selection.transforms.Length == 0 ? null : Selection.transforms[0], null, collaborationIdToJoin);
			}
		}

		EditorGUILayout.EndVertical();

		EditorGUILayout.Space();

		if (previousCollaborations != null)
		{
			EditorGUILayout.BeginVertical(GUI.groupStyle);

			GUILayout.Label("These are previous collaborations you participated in:", EditorStyles.wordWrappedLabel);
			EditorGUILayout.Space();

			previousCollaborationsScrollPos = GUILayout.BeginScrollView(previousCollaborationsScrollPos, EditorStyles.textArea, GUILayout.Height(100));
			for (int i = 0, max = previousCollaborations.Count; i < max; ++i)
			{
				CollaborationShortInfo collab = previousCollaborations[i];
				string label = $"{collab.id} by {collab.ownerDisplayName} ({collab.numberOfParticipants} participant(s)";

				if (GUILayout.Button(label, selectedPreviousCollaboration == i ? GUI.flatButtonStyleSelected : GUI.flatButtonStyle))
				{
					selectedPreviousCollaboration = selectedPreviousCollaboration == i ? -1 : i;
				}
			}
			GUILayout.EndScrollView();
			EditorGUILayout.Space();

			UnityEngine.GUI.enabled = isEnabled && previousCollaborations != null && selectedPreviousCollaboration > -1 && selectedPreviousCollaboration < previousCollaborations.Count;
			if (GUILayout.Button("Rejoin previous collaboration", GUILayout.Height(32)))
			{
				string collaborationId = previousCollaborations[selectedPreviousCollaboration].id;
				DebugUtils.Log(DebugContext.Collaboration, $"Attempting to rejoin previous collaboration with ID {collaborationId}");
				JoinCollaboration(Selection.transforms.Length == 0 ? null : Selection.transforms[0], null, collaborationId);
			}
			UnityEngine.GUI.enabled = isEnabled;

			EditorGUILayout.EndVertical();
		}

		UnityEngine.GUI.enabled = true;
	}

	private void RenderCollaborationGUI()
	{
		// Active collaboration warning

		EditorGUILayout.HelpBox("A collaboration is running!", MessageType.Warning);
		Rect pos = new(GUILayoutUtility.GetLastRect());
		Rect buttonRect = new(pos.xMax - 60, pos.yMin + (pos.height - 20) / 2, 55, 20);
		if (UnityEngine.GUI.Button(buttonRect, "Ping"))
		{
			Selection.objects = new[]{ collaborationState.collaborationObject };
			SceneView.lastActiveSceneView.FrameSelected();
			DebugUtils.Log(DebugContext.General, "Pinging game object", collaborationState.collaborationObject);
		}
		EditorGUILayout.Space();

		// Collaboration settings

		GUILayout.BeginVertical(GUI.groupStyle);

		GUILayout.Label("Please share this ID with others to join this collaboration:");

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.TextField(collaborationState.collaborationId, EditorStyles.textField, GUILayout.ExpandWidth(true));
		if (GUILayout.Button("Copy", GUILayout.ExpandWidth(false)))
		{
			GUIUtility.systemCopyBuffer = $"{collaborationState.collaborationId}";
			Debug.Log("Collaboration ID copied to clipboard!");
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		GUILayout.Label("Participants", EditorStyles.boldLabel);
		participantsScrollPos = GUILayout.BeginScrollView(participantsScrollPos, EditorStyles.textArea, GUILayout.Height(100));

		for (int i = 0; i < collaborationState.participants.Count; ++i)
		{
			Participant participant = collaborationState.participants[i];
			string name = participant.displayName;
			if (participant.id == owner.Settings.userId)
			{
				name += " (you)";
			}
			if (participant.id == collaborationState.collaborationOwnerId)
			{
				name += " (owner)";
			}
			if (participant.sids != null)
			{
				name += $" ({participant.sids.Count} session(s))";
			}

			UnityEngine.GUI.enabled = participant.sids != null && participant.sids.Count > 0;
			if (GUILayout.Button(name, selectedParticipant == i ? GUI.flatButtonStyleSelected : GUI.flatButtonStyle))
			{
				selectedParticipant = selectedParticipant == i ? -1 : i;

				if (selectedParticipant == i)
				{
					string sids = participant.sids == null ? "" : string.Join(", ", participant.sids);
					DebugUtils.Log(DebugContext.Collaboration, $"Participant ID {participant.id} with SIDs: {sids}");
				}
			}
			UnityEngine.GUI.enabled = true;
		}

		GUILayout.EndScrollView();

		EditorGUILayout.Space();

		if (GUILayout.Button("Disconnect", GUILayout.Height(32)))
		{
			Disconnect();
		}

		GUILayout.EndVertical();

		EditorGUILayout.Space();

		// Activity log

		if (owner.Settings.showActivityLog && logger != null)
		{
			GUILayout.Label("Activity", EditorStyles.boldLabel);

			scrollPos = GUI.ScrollableSelectableLabel(scrollPos, logger.GetValue(), EditorStyles.textArea);
			if (GUILayout.Button("Clear"))
			{
				logger.Clear();
			}
		}

		EditorGUILayout.Space();

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("state.tree"))
		{
			string json = JsonConvert.SerializeObject(collaborationState.tree, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
			DebugUtils.Log(DebugContext.Collaboration, $"state.tree = \n{json}");
		}
		if (GUILayout.Button("state.tree.ids"))
		{
			string json = JsonConvert.SerializeObject(collaborationState.tree.ids, Formatting.Indented);
			DebugUtils.Log(DebugContext.Collaboration, $"state.tree.ids = \n{json}");
		}
		if (GUILayout.Button("marker.hierarchy"))
		{
			PrefabbyCollaborationMarker marker = collaborationState.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();
			string json = JsonConvert.SerializeObject(marker.hierarchy, Formatting.Indented, debugJsonConverters);
			DebugUtils.Log(DebugContext.Collaboration, $"marker.hierarchy = \n{json}");
		}
		GUILayout.EndHorizontal();
	}

	public void Tick250()
	{
		stompApi?.DispatchMessageQueue();
		collaborationController?.Tick250();
	}

	public void Tick1000()
	{
		collaborationController?.Tick1000();
	}

	private void StartCollaboration(Transform parent)
	{
		GameObject rootObject = new(defaultPrefabbyRootGameObjectName);
		rootObject.transform.parent = parent;

		PrefabDictionary prefabDictionary = new();
		JsonV1Serializer serializer = new(rootObject, prefabDictionary);
		SerializedTree tree = serializer.Serialize();
		CreateCollaborationRequest createCollaborationRequest = new()
		{
			representation = serializer.GetRepresentation(),
			content = tree
		};
		restApi.StartCollaboration(
			owner.Settings.accessKey,
			createCollaborationRequest,

			(CollaborationInfo collaborationInfo) => {
				collaborationState = ScriptableObject.CreateInstance<CollaborationState>();
				collaborationState.collaborationId = collaborationInfo.id;
				collaborationState.collaborationOwnerId = collaborationInfo.ownerId;
				collaborationState.participants = collaborationInfo.participants;
				collaborationState.collaborationObject = rootObject;
				collaborationState.prefabDictionary = new();
				collaborationState.pendingSelectionChanges = new();
				collaborationState.tree = tree;

				EditorUtils.AttachCollaborationMarkerToGameObject(rootObject, collaborationInfo.id);

				Selection.activeGameObject = rootObject;

				InitializeForCollaboration();
			},
			() => {
				Object.DestroyImmediate(rootObject);
				EditorUtils.GenericError();
			}
		);
	}

	private void JoinCollaboration(Transform parent, Transform existingCollaboration, string collaborationId)
	{
		restApi.GetCollaboration(
			owner.Settings.accessKey,
			collaborationId,
			(CollaborationInfo collaborationInfo) => {
				List<ArtPackHint> availableArtPacks = EditorUtils.GetAvailableArtPacks(DebugContext.Collaboration, collaborationInfo.requiredArtPacks);
				if (availableArtPacks.Count != collaborationInfo.requiredArtPacks.Count)
				{
					List<ArtPackHint> missingArtPacks = collaborationInfo.requiredArtPacks.Except(availableArtPacks).ToList();

					RequiredArtPacksWindow.Show(
						missingArtPacks,
						owner.Settings,
						"The following packs are required for this collaboration but seem to be missing in the project. You cannot join the collaboration without these art packs installed."
					);
				}
				else
				{
					JoinCollaborationAfterTest(parent, existingCollaboration, collaborationId);
				}
			},
			() => {
				EditorUtils.GenericError();
			}
		);
	}

	private void JoinCollaborationAfterTest(Transform parent, Transform existingCollaboration, string collaborationId)
	{
		DebugUtils.Log(DebugContext.Collaboration, $"Joining collaboration after successful test for art packs");

		restApi.JoinCollaboration(
			owner.Settings.accessKey,
			collaborationId,
			(CollaborationInfo collaborationInfo) => {
				DebugUtils.Log(DebugContext.Collaboration, $"Joined collaboration with ID: {collaborationInfo.id}");

				GameObject go;
				if (existingCollaboration != null)
				{
					go = existingCollaboration.gameObject;
				}
				else
				{
					go = new GameObject(defaultPrefabbyRootGameObjectName);
					go.transform.parent = parent;
				}

				collaborationState = ScriptableObject.CreateInstance<CollaborationState>();
				collaborationState.collaborationId = collaborationId;
				collaborationState.collaborationOwnerId = collaborationInfo.ownerId;
				collaborationState.participants = collaborationInfo.participants;
				collaborationState.collaborationObject = go;
				collaborationState.prefabDictionary = collaborationInfo.prefabDictionary;
				collaborationState.pendingSelectionChanges = new();
				collaborationState.tree = collaborationInfo.content;

				if (existingCollaboration == null)
				{
					EditorUtils.AttachCollaborationMarkerToGameObject(go, collaborationInfo.id);
				}

				InitializeForCollaboration();

				collaborationController.UpdateRootObject(collaborationInfo.content);
			},
			() => {
				EditorUtils.GenericError();
			}
		);
	}

	private void InitializeForCollaboration()
	{
		DebugUtils.Log(DebugContext.Collaboration, "CollaborationTabContent.InitializeForCollaboration...");

		Assert.IsNotNull(collaborationState, "collaborationState must not be null!");

		PrefabbyCollaborationMarker marker = collaborationState.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();
		Assert.IsNotNull(marker, "GameObject must have a PrefabbyCollaborationMarker component!");

		logger = new Logger();
		logger.OnLogChanged += HandleLogChanged;
		stompApi = new StompApi(logger, owner.Settings.apiHost, owner.Settings.accessKey, marker.collaborationId);
		stompApi.Connect();

		collaborationController = new CollaborationController(owner, collaborationState, restApi, stompApi, () => { Disconnect(); });
		collaborationController.Initialize();

		if (owner.Settings.showCollaborationLimitationsNotice)
		{
			EditorUtility.DisplayDialog(
				"Information",
				"Please note that Prefabby's collaboration capabilities are limited:\n\n" +
					"* You cannot bring in prefabs/models/materials from unknown art packs\n" +
					"* You cannot bring in your own prefab variants, even if based on known art packs\n" +
					"* You cannot unpack prefabs\n" +
					"* Any changes outside of transform and existing renderer materials won't be synchronized\n" +
					"\nI still hope it's useful to you! Please submit bug reports to matt@prefabby.com\n\n" +
					"You can disable this message in the settings.",
					"Ok"
			);
		}
	}

	private void Disconnect()
	{
		stompApi.Disconnect();
		collaborationController.Uninitialize();
		collaborationController = null;
		logger.OnLogChanged -= HandleLogChanged;
		collaborationState = null;
	}

	private void HandleLogChanged(string value)
	{
		if (!GUI.initialized)
		{
			return;
		}

		float pixelHeight = GUI.selectableLabelStyle.CalcHeight(new GUIContent(value), scrollPos.z);
		scrollPos.y = pixelHeight;

		owner.Repaint();
	}

}

}
