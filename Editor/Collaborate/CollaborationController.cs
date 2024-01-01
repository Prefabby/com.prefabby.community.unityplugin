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
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Prefabby
{

class CollaborationController
{

	private readonly EditorInstanceCreator editorInstanceCreator = new();
	private readonly EditorSerializationErrorHandler editorSerializationErrorHandler = new();

	private readonly PrefabbyCommunityWindow owner;
	private readonly CollaborationState state;
	private readonly EditorRestApi restApi;
	private readonly StompApi stompApi;
	private readonly PrefabIdentification prefabIdentification;
	private readonly Action stopAction;

	public CollaborationController(PrefabbyCommunityWindow owner, CollaborationState state, EditorRestApi restApi, StompApi stompApi, Action stopAction)
	{
		this.owner = owner;
		this.state = state;
		this.restApi = restApi;
		this.stompApi = stompApi;
		this.stopAction = stopAction;
		this.prefabIdentification = new PrefabIdentification(restApi, state.collaborationId, owner.Settings.accessKey);
	}

	#region Lifecycle methods

	public void Initialize()
	{
		DebugUtils.Log(DebugContext.Collaboration, "CollaborationController.Initialize...");

		state.selectionTransforms = new List<TransformCopy>();

		Selection.selectionChanged += HandleSelectionChange;

		PrefabbyCollaborationMarker marker = state.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();

		marker.settingsAccessor = owner.Settings;
		marker.SetTree(state.tree);

		marker.OnTransformAdded += HandleTransformAdded;
		marker.OnTransformChanged += HandleTransformChanged;
		marker.OnTransformRemoved += HandleTransformRemoved;
		marker.OnGameObjectActiveToggled += HandleGameObjectActiveToggled;
		marker.OnNameChanged += HandleNameChanged;
		marker.OnMaterialsChanged += HandleMaterialsChanged;
		marker.OnTransformReparented += HandleTransformReparented;

		stompApi.OnSync += HandleSyncMessage;
		stompApi.OnTransformsChanged += HandleTransformsChangedMessage;
		stompApi.OnSelectionChanged += HandleSelectionChangedMessage;
		stompApi.OnConnect += HandleConnectMessage;
		stompApi.OnDisconnect += HandleDisconnectMessage;
		stompApi.OnPrefabDictionaryItemsAdded += HandlePrefabDictionaryItemsAddedMessage;
		stompApi.OnTransformAdded += HandleTransformAddedMessage;
		stompApi.OnTransformRemoved += HandleTransformRemovedMessage;
		stompApi.OnTransformReparented += HandleTransformReparentedMessage;
		stompApi.OnMaterialsChanged += HandleMaterialsChangedMessage;
	}

	public void Uninitialize()
	{
		DebugUtils.Log(DebugContext.Collaboration, "CollaborationController.Uninitialize...");

		Selection.selectionChanged -= HandleSelectionChange;

		// Safeguard for the case that the user deletes the object before disconnecting from the collaboration
		if (state.collaborationObject != null && state.collaborationObject.TryGetComponent(out PrefabbyCollaborationMarker marker))
		{
			marker.settingsAccessor = null;
			marker.SetTree(null);

			marker.OnTransformAdded -= HandleTransformAdded;
			marker.OnTransformChanged -= HandleTransformChanged;
			marker.OnTransformRemoved -= HandleTransformRemoved;
			marker.OnGameObjectActiveToggled -= HandleGameObjectActiveToggled;
			marker.OnNameChanged -= HandleNameChanged;
			marker.OnTransformReparented -= HandleTransformReparented;
		}

		stompApi.OnSync -= HandleSyncMessage;
		stompApi.OnTransformsChanged -= HandleTransformsChangedMessage;
		stompApi.OnSelectionChanged -= HandleSelectionChangedMessage;
		stompApi.OnConnect -= HandleConnectMessage;
		stompApi.OnDisconnect -= HandleDisconnectMessage;
		stompApi.OnPrefabDictionaryItemsAdded -= HandlePrefabDictionaryItemsAddedMessage;
		stompApi.OnTransformAdded -= HandleTransformAddedMessage;
		stompApi.OnTransformRemoved -= HandleTransformRemovedMessage;
		stompApi.OnTransformReparented -= HandleTransformReparentedMessage;
		stompApi.OnMaterialsChanged -= HandleMaterialsChangedMessage;
	}

	public void Tick250()
	{
		// Nothing to do
	}

	public void Tick1000()
	{
		CheckAndSendTransformUpdates();
	}

	#endregion

	private void RefreshSceneView()
	{
		if (owner.Settings.forceRefresh)
		{
			EditorWindow view = EditorWindow.GetWindow<SceneView>();
			view.Repaint();
		}
	}

	#region Marker event handlers

	private void HandleTransformAdded(Transform transform)
	{
		Assert.IsNotNull(transform);

		DebugUtils.Log(DebugContext.TransformAddedOutgoing, $"HandleTransformAdded {transform.name}");

		// Any added prefab(s) need to be identified; if the identification fails, they will be removed immediately.
		prefabIdentification.IdentifyPrefabs(
			transform,
			state.prefabDictionary,
			() => {
				SendTransformsAdded(transform);
			},
			(ErrorInfo errorInfo) => {
				EditorUtils.HandlePrefabIdentificationError(errorInfo);

				if (errorInfo?.errorCode == "UnknownPrefab")
				{
					DebugUtils.Log(DebugContext.PrefabIdentification, $"Prefab of transform {transform.name} could not be identified, removing newly added object...");

					// Check if this is currently selected and tracked; remove if necessary
					state.selectionTransforms.RemoveAll(transformCopy => transformCopy.Source == transform.gameObject);

					RunHierarchyMutatingChange(() => {
						UnityEngine.Object.DestroyImmediate(transform.gameObject);
					});

					RefreshSceneView();
				}
			}
		);
	}

	private void SendTransformsAdded(Transform root)
	{
		Assert.IsNotNull(root);

		DebugUtils.Log(DebugContext.TransformAddedOutgoing, $"Preparing transform added message for transform {root.name}");

		(SerializedGameObject parentSGO, GameObject parentGO) = state.tree.FindClosestParent(root.gameObject);

		JsonV1Serializer serializer = new(root.gameObject, state.prefabDictionary);
		SerializedTree tree = serializer.Serialize();

		// Adjust the root SGO path of this subtree if it's not a direct child
		SerializedGameObject rootGO = tree.FindById(tree.root);
		rootGO.path = root.parent == parentGO.transform ? null : EditorUtils.CreatePath(root.parent, parentGO.transform);

		state.tree.gameObjects.AddRange(tree.gameObjects);

		parentSGO.children ??= new();
		parentSGO.children.Add(tree.root);

		foreach (var kvp in tree.ids)
		{
			state.tree.ids.Add(kvp.Key, kvp.Value);
		}

		DebugUtils.Log(DebugContext.TransformAddedOutgoing, $"Sending transform added message for transform {root.name}");

		TransformAddedMessage transformAddedMessage = new()
		{
			parentId = parentSGO.id,
			tree = tree
		};

		stompApi.SendMessage(transformAddedMessage);
	}

	private void HandleTransformChanged(Transform transform, Vector3? position, Quaternion? rotation, Vector3? scale)
	{
		// Check if this is currently selected and tracked; if yes, the incoming change is the new name
		foreach (TransformCopy transformCopy in state.selectionTransforms)
		{
			if (transformCopy.Source == transform)
			{
				if (position != null)
				{
					transformCopy.Position = (Vector3) position;
				}
				if (rotation != null)
				{
					transformCopy.Rotation = (Quaternion) rotation;
				}
				if (scale != null)
				{
					transformCopy.Scale = (Vector3) scale;
				}
				break;
			}
		}

		(SerializedGameObject sgo, string parentId, string path) = GetOrCreateSerializedGameObjectOutgoing(transform.gameObject);

		TransformsChangedMessage transformsChangedMessage = new()
		{
			transforms = new List<TransformsChangedMessage.Transform>()
			{
				new TransformsChangedMessage.Transform()
				{
					id = sgo.id,
					parentId = parentId,
					path = path,
					name = transform.name,
					position = position != null ? new SerializedVector((Vector3)position) : null,
					rotation = rotation != null ? new SerializedVector(((Quaternion)rotation).eulerAngles) : null,
					scale = scale != null ? new SerializedVector((Vector3)scale) : null
				}
			}
		};
		stompApi.SendMessage(transformsChangedMessage);
	}

	private void HandleTransformRemoved(string id, string parentId, string path, string name)
	{
		DebugUtils.Log(DebugContext.Collaboration, $"Handling removal of SGO with ID={id}, parentId={parentId}, path={path}, name={name}...");

		// Update tracked selection
		state.selectionTransforms.RemoveAll(
			// If the Source of a TransformCopy becomes null, the object was deleted.
			transformCopy => transformCopy.Source == null ||
			// If it's no longer a child of the collaboration object, it was moved out of the tree.
			!transformCopy.Source.transform.IsChildOf(state.collaborationObject.transform
		));

		bool? mark = null;

		if (id != null)
		{
			// We already know this object; parentId and path will be null

			SerializedGameObject sgo = state.tree.FindById(id);
			GameObject go = state.tree.FindGameObjectById(id);

			if (sgo.prefab != null || !PrefabUtility.IsPartOfAnyPrefab(go))
			{
				DebugUtils.Log(DebugContext.Collaboration, $"SGO ID {id} and its children will be removed from the tree entirely");

				// If this is not a child of a prefab, we can remove it entirely
				state.tree.RemoveById(id);
			}
			else
			{
				DebugUtils.Log(DebugContext.Collaboration, $"SGO ID {id} will be marked as deleted");

				// This is a child of a prefab, mark it as deleted
				sgo.status = SerializedGameObjectStatus.Deleted;
				mark = true;
			}
		}
		else
		{
			// We don't know this object yet, create an SGO using the parent and path

			DebugUtils.Log(DebugContext.Collaboration, $"SGO is not yet known and needs to be added to the tree");

			SerializedGameObject parentSGO = state.tree.FindById(parentId);

			id = Guid.NewGuid().ToString("N");

			parentSGO.children ??= new();
			parentSGO.children.Add(id);

			SerializedGameObject sgo = new()
			{
				id = id,
				status = SerializedGameObjectStatus.Deleted,
				path = path,
				name = name
			};

			state.tree.gameObjects.Add(sgo);
			// it cannot be added to the ids though, as there's no GO: state.tree.ids.Add(go, id);
		}

		TransformRemovedMessage transformRemovedMessage = new()
		{
			id = id,
			mark = mark,
			parentId = parentId,
			path = path
		};
		stompApi.SendMessage(transformRemovedMessage);
	}

	private void HandleGameObjectActiveToggled(Transform transform)
	{
		(SerializedGameObject sgo, string parentId, string path) = GetOrCreateSerializedGameObjectOutgoing(transform.gameObject);

		TransformsChangedMessage transformsChangedMessage = new()
		{
			transforms = new List<TransformsChangedMessage.Transform>()
			{
				new TransformsChangedMessage.Transform()
				{
					id = sgo.id,
					parentId = parentId,
					path = path,
					status = transform.gameObject.activeSelf ? SerializedGameObjectStatus.Active : SerializedGameObjectStatus.Inactive
				}
			}
		};
		stompApi.SendMessage(transformsChangedMessage);
	}

	private void HandleNameChanged(Transform transform, string name)
	{
		// Check if this is currently selected and tracked; if yes, the incoming change is the new name
		foreach (TransformCopy transformCopy in state.selectionTransforms)
		{
			if (transformCopy.Source == transform)
			{
				transformCopy.Name = name;
				break;
			}
		}

		(SerializedGameObject sgo, string parentId, string path) = GetOrCreateSerializedGameObjectOutgoing(transform.gameObject);

		// Publish the change
		TransformsChangedMessage transformsChangedMessage = new()
		{
			transforms = new List<TransformsChangedMessage.Transform>()
			{
				new TransformsChangedMessage.Transform()
				{
					id = sgo.id,
					parentId = parentId,
					path = path,
					name = name
				}
			}
		};
		stompApi.SendMessage(transformsChangedMessage);
	}

	private void HandleMaterialsChanged(Transform transform, List<int> slots)
	{
		// Try to identify the materials and collect those which require identification

		Renderer renderer = transform.GetComponent<Renderer>();
		Material[] materials = renderer.sharedMaterials;

		List<IdentifyPrefabsRequest.IdentifyPrefabRequest> identifyPrefabRequests = new();

		foreach (int slot in slots)
		{
			(string path, string name) = EditorUtils.GetPathAndNameFromMaterial(materials[slot]);

			PrefabDictionaryItem item = state.prefabDictionary.Resolve(path, name);
			if (item == null)
			{
				identifyPrefabRequests.Add(new IdentifyPrefabsRequest.IdentifyPrefabRequest
				{
					name = name,
					path = path,
					hint = "Material"
				});
			}
		}

		if (identifyPrefabRequests.Count > 0)
		{
			// Try to resolve the prefab

			IdentifyPrefabsRequest identifyPrefabsRequest = new()
			{
				prefabs = identifyPrefabRequests
			};

			restApi.IdentifyPrefabsForCollaboration(
				owner.Settings.accessKey,
				state.collaborationId,
				identifyPrefabsRequest,
				(List<PrefabDictionaryItem> result) => {
					foreach (PrefabDictionaryItem item in result)
					{
						state.prefabDictionary.Add(item);
					}
					SendMaterialsChanged(transform, slots);
				},
				(ErrorInfo errorInfo) => {
					EditorUtils.HandlePrefabIdentificationError(errorInfo);
				}
			);
		}
		else
		{
			SendMaterialsChanged(transform, slots);
		}
	}

	private void SendMaterialsChanged(Transform transform, List<int> slots)
	{
		Renderer renderer = transform.GetComponent<Renderer>();
		Material[] materials = renderer.sharedMaterials;

		(SerializedGameObject sgo, string parentId, string path) = GetOrCreateSerializedGameObjectOutgoing(transform.gameObject);

		sgo.materials ??= new();

		List<MaterialsChangedMessage.MaterialChange> changes = new();
		foreach (int slot in slots)
		{
			(string materialPath, string materialName) = EditorUtils.GetPathAndNameFromMaterial(materials[slot]);
			PrefabDictionaryItem item = state.prefabDictionary.Resolve(materialPath, materialName);
			changes.Add(new MaterialsChangedMessage.MaterialChange
			{
				slot = slot,
				id = item.id,
				name = materialName
			});

			sgo.UpdateMaterial(slot, item.id, materialName);
		}

		MaterialsChangedMessage materialsChangedMessage = new()
		{
			id = sgo.id,
			parentId = parentId,
			path = path,
			changes = changes
		};
		stompApi.SendMessage(materialsChangedMessage);
	}

	public void HandleTransformReparented(Transform transform, Transform oldParent)
	{
		string id = state.tree.ids[transform.gameObject];
		Assert.IsNotNull(id);
		string newParentId = state.tree.ids[transform.parent.gameObject];
		Assert.IsNotNull(newParentId);

		SerializedGameObject oldParentSGO = state.tree.FindParentOf(id);
		SerializedGameObject newParentSGO = state.tree.FindById(newParentId);

		Assert.IsTrue(oldParentSGO.children != null && oldParentSGO.children.Contains(id));
		oldParentSGO.children.Remove(id);

		newParentSGO.children ??= new();
		newParentSGO.children.Add(id);

		TransformReparentedMessage transformReparentedMessage = new()
		{
			id = id,
			newParentId = newParentId,
			siblingIndex = transform.GetSiblingIndex()
		};
		stompApi.SendMessage(transformReparentedMessage);
	}

	#endregion

	#region Selection handler

	private void HandleSelectionChange()
	{
		CheckAndSendTransformUpdates();

		state.selectionTransforms = new();
		List<string> selection = new();
		foreach (Transform transform in Selection.transforms)
		{
			if (transform.IsChildOf(state.collaborationObject.transform))
			{
				string path = CreatePath(transform);

				selection.Add(path);

				TransformCopy transformCopy = new()
				{
					Source = transform.gameObject,
					Path = path,
					Position = transform.localPosition,
					Rotation = transform.rotation,
					Scale = transform.localScale,
					Name = transform.name
				};
				state.selectionTransforms.Add(transformCopy);

				DebugUtils.Log(DebugContext.Selection, $"Added GO '{transformCopy.Name}' to selection transforms, the list now contains {state.selectionTransforms.Count} transforms", transformCopy.Source);
			}
		}

		if (state.selectionTransforms.Count == 0)
		{
			DebugUtils.Log(DebugContext.Selection, "Nothing selected in collaboration hierarchy");
		}

		SelectionChangedMessage selectionChangedMessage = new()
		{
			paths = selection
		};
		stompApi.SendMessage(selectionChangedMessage);
	}

	private void CheckAndSendTransformUpdates()
	{
		if (state.selectionTransforms != null)
		{
			List<TransformsChangedMessage.Transform> transforms = new();
			foreach (TransformCopy transformCopy in state.selectionTransforms)
			{
				if (transformCopy.Source == null)
				{
					DebugUtils.Log(DebugContext.TransformChangesOutgoing, $"TransformCopy {transformCopy.Name} at path {transformCopy.Path} has become invalid, skipping...");
					continue;
				}

				if (transformCopy.Position != transformCopy.Source.transform.localPosition ||
					transformCopy.Rotation != transformCopy.Source.transform.rotation ||
					transformCopy.Scale != transformCopy.Source.transform.localScale ||
					transformCopy.Name != transformCopy.Source.name)
				{
					(SerializedGameObject sgo, string parentId, string path) = GetOrCreateSerializedGameObjectOutgoing(transformCopy.Source);

					TransformsChangedMessage.Transform t = new()
					{
						id = sgo.id,
						path = path,
						parentId = parentId
					};
					if (transformCopy.Position != transformCopy.Source.transform.localPosition)
					{
						sgo.position = t.position = new SerializedVector(transformCopy.Source.transform.localPosition);
						transformCopy.Position = transformCopy.Source.transform.localPosition;
					}
					if (transformCopy.Rotation != transformCopy.Source.transform.rotation)
					{
						sgo.rotation = t.rotation = new SerializedVector(transformCopy.Source.transform.localEulerAngles);
						transformCopy.Rotation = transformCopy.Source.transform.rotation;
					}
					if (transformCopy.Scale != transformCopy.Source.transform.localScale)
					{
						sgo.scale = t.scale = new SerializedVector(transformCopy.Source.transform.localScale);
						transformCopy.Scale = transformCopy.Source.transform.localScale;
					}
					if (transformCopy.Name != transformCopy.Source.name)
					{
						sgo.name = t.name = transformCopy.Source.name;
						transformCopy.Name = transformCopy.Source.name;
					}

					transforms.Add(t);
				}
			}
			if (transforms.Count > 0)
			{
				TransformsChangedMessage transformsChangedMessage = new()
				{
					transforms = transforms
				};
				stompApi.SendMessage(transformsChangedMessage);
			}
		}
	}

	#endregion

	#region Stompi API message handlers

	private void HandleSyncMessage(SyncMessage syncMessage)
	{
		state.prefabDictionary = syncMessage.prefabDictionary;
		state.participants = syncMessage.participants;

		UpdateRootObject(syncMessage.content);
	}

	public void UpdateRootObject(SerializedTree tree)
	{
		JsonV1Deserializer deserializer = new(new EditorInstanceCreator(), new EditorSerializationErrorHandler(), tree, state.prefabDictionary);
		Transform created = deserializer.Deserialize(null);

		RunHierarchyMutatingChange(() => {
			// Remove all existing children from the collaboration object
			while (state.collaborationObject.transform.childCount > 0)
			{
				UnityEngine.Object.DestroyImmediate(state.collaborationObject.transform.GetChild(0).gameObject);
			}
			// Reparent the children of the created object instead
			while (created.childCount > 0)
			{
				created.GetChild(0).parent = state.collaborationObject.transform;
			}
			// For the root, only take over the name
			state.collaborationObject.name = created.name;

			// Take over the incoming tree, only the root needs to be adjusted manually
			state.tree = tree;
			state.tree.ids[state.collaborationObject] = tree.root;
			state.tree.ids.Remove(created.gameObject);

			// Destroy the created root object again, we don't need it anymore
			UnityEngine.Object.DestroyImmediate(created.gameObject);
		});
	}

	private void HandleTransformsChangedMessage(TransformsChangedMessage transformsChangedMessage)
	{
		foreach (TransformsChangedMessage.Transform transformChange in transformsChangedMessage.transforms)
		{
			(SerializedGameObject sgo, GameObject go) = GetOrCreateSerializedGameObjectIncoming(transformChange.id, transformChange.parentId, transformChange.path);

			DebugUtils.Log(DebugContext.TransformChangesIncoming, $"Handling transform change for path {transformChange.path} ===> object {go.name}");

			// Check if this is currently selected and tracked; if yes, the incoming changes are the new transform;
			// otherwise the changes would be detected on this client's side, too, and broadcast back, causing an infinite loop
			TransformCopy copyToChange = null;
			foreach (TransformCopy transformCopy in state.selectionTransforms)
			{
				if (transformCopy.Source == go)
				{
					copyToChange = transformCopy;
					break;
				}
			}

			// Now apply the inidividual changes to 1. the actual transform, 2. the SGO, and 3. a tracked copy during selection.
			if (transformChange.position != null)
			{
				go.transform.localPosition = transformChange.position.ToVector3();
				sgo.position = transformChange.position;
				if (copyToChange != null)
				{
					copyToChange.Position = go.transform.localPosition;
				}
			}
			if (transformChange.rotation != null)
			{
				go.transform.localRotation = Quaternion.Euler(transformChange.rotation.ToVector3());
				sgo.rotation = transformChange.rotation;
				if (copyToChange != null)
				{
					copyToChange.Rotation = go.transform.rotation;
				}
			}
			if (transformChange.scale != null)
			{
				go.transform.localScale = transformChange.scale.ToVector3();
				sgo.scale = transformChange.scale;
				if (copyToChange != null)
				{
					copyToChange.Scale = go.transform.localScale;
				}
			}
			if (transformChange.name != null)
			{
				go.transform.name = transformChange.name;
				sgo.name = transformChange.name;
				if (copyToChange != null)
				{
					copyToChange.Name = go.transform.name;
				}
			}
			if (transformChange.status is SerializedGameObjectStatus status)
			{
				if (status == SerializedGameObjectStatus.Active)
				{
					go.SetActive(true);
				}
				else if (status == SerializedGameObjectStatus.Inactive)
				{
					go.SetActive(false);
				}
			}
		}

		RefreshSceneView();
	}

	private void StorePendingSelectionChange(string sid, string path)
	{
		if (state.pendingSelectionChanges.TryGetValue(sid, out List<string> pendingSelectionChanges))
		{
			if (!pendingSelectionChanges.Contains(path))
			{
				pendingSelectionChanges.Add(path);
			}
		}
		else
		{
			pendingSelectionChanges = new()
			{
				path
			};
			state.pendingSelectionChanges.Add(sid, pendingSelectionChanges);
		}
	}

	private void TryApplyPendingSelectionChanges(string sid)
	{
		if (!state.pendingSelectionChanges.TryGetValue(sid, out List<string> pendingSelectionChanges))
		{
			return;
		}

		PrefabbyCollaborationMarker marker = state.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();

		if (!marker.otherParticipantSelections.TryGetValue(sid, out List<Transform> selections))
		{
			selections = new();
			marker.otherParticipantSelections.Add(sid, selections);
		}

		foreach (string path in pendingSelectionChanges)
		{
			if (IsValidPath(path))
			{
				selections.Add(CommonUtils.FindWithPath(state.collaborationObject.transform, path));
				DebugUtils.Log(DebugContext.Selection, $"Pending selection path {path} for SID {sid} successfully applied.");
			}
			else
			{
				DebugUtils.Log(DebugContext.Selection, $"Pending selection path {path} for SID {sid} could not be applied!");
			}
		}

		state.pendingSelectionChanges.Remove(sid);
	}

	private void HandleSelectionChangedMessage(SelectionChangedMessage selectionChangedMessage)
	{
		PrefabbyCollaborationMarker marker = state.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();

		Participant participant = state.participants.Find(participant => participant.id == selectionChangedMessage.origin);
		marker.MapSidToParticipant(selectionChangedMessage.sid, participant.id, participant.displayName);

		if (!marker.otherParticipantSelections.TryGetValue(selectionChangedMessage.sid, out List<Transform> selections))
		{
			selections = new List<Transform>();
			marker.otherParticipantSelections.Add(selectionChangedMessage.sid, selections);
		}
		else
		{
			selections.Clear();
		}

		foreach (string path in selectionChangedMessage.paths)
		{
			// If a new object was added and prefabs need to be identified first, the SelectionChangedMessage will arrive before the corresponding TransformAddedMessage;
			// for that reason, we keep selection changes and try to reapply them after a TransformAddedMessage.
			if (IsValidPath(path))
			{
				DebugUtils.Log(DebugContext.Selection, $"Adding path {path} to selections for SID {selectionChangedMessage.sid}");
				selections.Add(CommonUtils.FindWithPath(state.collaborationObject.transform, path));
			}
			else
			{
				DebugUtils.Log(DebugContext.Selection, $"Selection path {path} for SID {selectionChangedMessage.sid} cannot be used yet, storing as pending...");
				StorePendingSelectionChange(selectionChangedMessage.sid, path);
			}
		}

		RefreshSceneView();
	}

	private void HandleConnectMessage(ConnectMessage connectMessage)
	{
		DebugUtils.Log(DebugContext.ConnectIncoming, $"HandleConnectMessage for ID {connectMessage.origin} ({connectMessage.displayName}) with SID {connectMessage.sid}");

		Participant candidate = state.participants.Find(participant => participant.id == connectMessage.origin);
		if (candidate != null)
		{
			candidate.sids ??= new();

			if (!candidate.sids.Contains(connectMessage.sid))
			{
				candidate.sids.Add(connectMessage.sid);
			}
		}
		else
		{
			state.participants.Add(new Participant
			{
				id = connectMessage.origin,
				displayName = connectMessage.displayName,
				sids = new()
				{
					connectMessage.sid
				}
			});
		}
	}

	private void HandleDisconnectMessage(BaseMessage message)
	{
		DebugUtils.Log(DebugContext.ConnectIncoming, $"HandleDisconnectMessage for ID {message.origin} with SID {message.sid}");

		Participant candidate = state.participants.Find(participant => participant.id == message.origin);
		if (candidate != null && candidate.sids != null)
		{
			candidate.sids.Remove(message.sid);
		}

		PrefabbyCollaborationMarker marker = state.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();
		marker.RemoveSid(message.sid);
	}

	private void HandlePrefabDictionaryItemsAddedMessage(PrefabDictionaryItemsAddedMessage prefabDictionaryItemsAddedMessage)
	{
		DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, "HandlePrefabDictionaryItemsAddedMessage");

		// If new packs are added to the dictionary by another user, test if the required packs are available locally, too:
		List<ArtPackHint> requiredArtPacks = new();
		foreach (PrefabDictionaryItem item in prefabDictionaryItemsAddedMessage.items)
		{
			if (item.hint != null)
			{
				requiredArtPacks.Add(item.hint);
			}
		}
		List<ArtPackHint> availableArtPacks = EditorUtils.GetAvailableArtPacks(DebugContext.Collaboration, requiredArtPacks);
		if (availableArtPacks.Count == requiredArtPacks.Count)
		{
			// Fine, we've got everything
			foreach (PrefabDictionaryItem item in prefabDictionaryItemsAddedMessage.items)
			{
				state.prefabDictionary.Add(item);
			}
		}
		else
		{
			// Something is missing, stop the collaboration
			stopAction();

			// Bring up the dialog
			List<ArtPackHint> missingArtPacks = requiredArtPacks.Except(availableArtPacks).ToList();
			RequiredArtPacksWindow.Show(
				missingArtPacks,
				owner.Settings,
				"The following packs are required for this collaboration but seem to be missing in the project. You have been disconnected from the collaboration."
			);
		}
	}

	private void HandleTransformAddedMessage(TransformAddedMessage transformAddedMessage)
	{
		Assert.IsNotNull(transformAddedMessage);

		DebugUtils.Log(DebugContext.TransformAddedIncoming, "HandleTransformAddedMessage");

		JsonV1Deserializer deserializer = new(editorInstanceCreator, editorSerializationErrorHandler, transformAddedMessage.tree, state.prefabDictionary);

		RunHierarchyMutatingChange(() => {
			GameObject parent = state.tree.FindGameObjectById(transformAddedMessage.parentId);
			Assert.IsNotNull(parent);

			deserializer.Deserialize(parent.transform);

			state.tree.gameObjects.AddRange(transformAddedMessage.tree.gameObjects);

			foreach (var kvp in transformAddedMessage.tree.ids)
			{
				state.tree.ids.Add(kvp.Key, kvp.Value);
			}
		});

		// Now try to apply pending selections, if any
		TryApplyPendingSelectionChanges(transformAddedMessage.sid);

		RefreshSceneView();
	}

	private void HandleTransformRemovedMessage(TransformRemovedMessage transformRemovedMessage)
	{
		DebugUtils.Log(DebugContext.TransformRemovedIncoming, "HandleTransformRemovedMessage");

		RunHierarchyMutatingChange(() => {
			if (transformRemovedMessage.parentId == null)
			{
				// This SGO should already be known by ID

				GameObject go = state.tree.FindGameObjectById(transformRemovedMessage.id);
				UnityEngine.Object.DestroyImmediate(go);

				SerializedGameObject sgo = state.tree.FindById(transformRemovedMessage.id);
				if (transformRemovedMessage.mark == null || !(bool) transformRemovedMessage.mark)
				{
					state.tree.RemoveById(transformRemovedMessage.id);
				}
				else
				{
					sgo.status = SerializedGameObjectStatus.Deleted;
				}
			}
			else
			{
				// This isn't, so we need to add it accordingly

				SerializedGameObject parentSGO = state.tree.FindById(transformRemovedMessage.parentId);

				parentSGO.children ??= new();
				parentSGO.children.Add(transformRemovedMessage.id);

				state.tree.gameObjects.Add(
					new()
					{
						id = transformRemovedMessage.id,
						path = transformRemovedMessage.path,
						name = transformRemovedMessage.name
					}
				);

				GameObject parentGO = state.tree.FindGameObjectById(transformRemovedMessage.parentId);
				Transform child = CommonUtils.FindWithPath(parentGO.transform, transformRemovedMessage.path);
				UnityEngine.Object.DestroyImmediate(child.gameObject);
			}
		});

		RefreshSceneView();
	}

	private void HandleTransformReparentedMessage(TransformReparentedMessage transformReparentedMessage)
	{
		DebugUtils.Log(DebugContext.TransformReparentedIncoming, "HandleTransformReparentedMessage");

		RunHierarchyMutatingChange(() => {
			SerializedGameObject sgo = state.tree.FindById(transformReparentedMessage.id);
			Assert.IsNotNull(sgo);
			GameObject go = state.tree.FindGameObjectById(transformReparentedMessage.id);
			Assert.IsNotNull(go);

			SerializedGameObject oldParentSGO = state.tree.FindParentOf(transformReparentedMessage.id);
			Assert.IsNotNull(oldParentSGO);

			SerializedGameObject newParentSGO = state.tree.FindById(transformReparentedMessage.newParentId);
			Assert.IsNotNull(newParentSGO);
			GameObject newParentGO = state.tree.FindGameObjectById(transformReparentedMessage.newParentId);
			Assert.IsNotNull(newParentSGO);

			oldParentSGO.RemoveChild(transformReparentedMessage.id);
			newParentSGO.AddChild(transformReparentedMessage.id);

			go.transform.SetParent(newParentGO.transform);
			go.transform.SetSiblingIndex(transformReparentedMessage.siblingIndex);
		});

		RefreshSceneView();
	}

	private void HandleMaterialsChangedMessage(MaterialsChangedMessage materialsChangedMessage)
	{
		DebugUtils.Log(DebugContext.MaterialsChangedIncoming, "HandleMaterialsChangedMessage");

		RunHierarchyMutatingChange(() => {
			JsonV1Deserializer deserializer = new(editorInstanceCreator, editorSerializationErrorHandler, state.tree, state.prefabDictionary);

			(SerializedGameObject sgo, GameObject go) = GetOrCreateSerializedGameObjectIncoming(materialsChangedMessage.id, materialsChangedMessage.parentId, materialsChangedMessage.path);
			if (go != null)
			{
				if (go.TryGetComponent<Renderer>(out var renderer))
				{
					Material[] newMaterials = (Material[]) renderer.sharedMaterials.Clone();
					foreach (MaterialsChangedMessage.MaterialChange materialChange in materialsChangedMessage.changes)
					{
						// Update the actual material
						newMaterials[materialChange.slot] = deserializer.FindMaterial(state.prefabDictionary, materialChange.id, materialChange.name);

						// Update the SGO
						sgo.UpdateMaterial(materialChange.slot, materialChange.id, materialChange.name);
					}
					renderer.sharedMaterials = newMaterials;
				}
			}
		});

		RefreshSceneView();
	}

	#endregion

	private string CreatePath(Transform transform)
	{
		return EditorUtils.CreatePath(transform, state.collaborationObject.transform);
	}

	private bool IsValidPath(string path)
	{
		Transform node = state.collaborationObject.transform;
		string[] nodeSplits = path.Split("/n[", StringSplitOptions.RemoveEmptyEntries);
		foreach (string nodeSplit in nodeSplits)
		{
			string[] nameSplits = nodeSplit.Split("]i[", StringSplitOptions.RemoveEmptyEntries);
			string indexAsString = nameSplits[1][0..(nameSplits[1].Length - 1)];
			int index = int.Parse(indexAsString);
			if (index < node.childCount)
			{
				Transform child = node.GetChild(int.Parse(indexAsString));
				if (child.name != NameEncoder.Decode(nameSplits[0]))
				{
					return false;
				}
				node = child;
			}
			else
			{
				return false;
			}
		}
		return node != null;
	}

	private void RunHierarchyMutatingChange(Action action)
	{
		Assert.IsNotNull(action);

		// The action mutates the marker's tracked hierarchy from outside, therefore we need to rebuild it after this change.
		// First check if we have local changes that need to be published, otherwise they would remain undetected after the reset below.
		PrefabbyCollaborationMarker marker = state.collaborationObject.GetComponent<PrefabbyCollaborationMarker>();
		marker.FindHierarchyChanges();

		action();

		// Rebuild hierarchy
		marker.ResetHierarchy();
	}

	private (SerializedGameObject sgo, string parentId, string path) GetOrCreateSerializedGameObjectOutgoing(GameObject go)
	{
		string path = null;
		string parentId = null;
		SerializedGameObject sgo;

		// If we don't have an SGO for this GO yet, create one
		if (!state.tree.ids.TryGetValue(go, out string id))
		{
			(SerializedGameObject parentSGO, GameObject parentGO) = state.tree.FindClosestParent(go);
			id = Guid.NewGuid().ToString("N");
			path = EditorUtils.CreatePath(go.transform, parentGO.transform);
			parentId = parentSGO.id;

			parentSGO.children ??= new();
			parentSGO.children.Add(id);

			sgo = new()
			{
				id = id,
				path = path,
				name = go.name
			};

			state.tree.gameObjects.Add(sgo);
			state.tree.ids.Add(go, id);
		}
		else
		{
			sgo = state.tree.FindById(id);
		}

		return (sgo, parentId, path);
	}

	private (SerializedGameObject sgo, GameObject go) GetOrCreateSerializedGameObjectIncoming(string id, string parentId, string path)
	{
		Transform transform;

		// Check if we already know the modified GO as an SGO
		SerializedGameObject sgo = state.tree.FindById(id);
		if (sgo == null) {
			// We don't, so find the parent and create an SGO for the target
			GameObject parentGO = state.tree.FindGameObjectById(parentId);
			transform = CommonUtils.FindWithPath(parentGO.transform, path);

			sgo = new()
			{
				id = id,
				path = path,
			};
			state.tree.gameObjects.Add(sgo);
			state.tree.ids[transform.gameObject] = id;

			SerializedGameObject parentSGO = state.tree.FindById(parentId);
			parentSGO.children ??= new();
			parentSGO.children.Add(id);
		}
		else
		{
			// We do, use it
			transform = state.tree.FindGameObjectById(id).transform;
		}

		return (sgo, transform.gameObject);
	}

}

}
