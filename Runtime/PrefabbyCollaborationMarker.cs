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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Prefabby
{

[ExecuteInEditMode]
public class PrefabbyCollaborationMarker : MonoBehaviour
#if UNITY_EDITOR
, ISerializationCallbackReceiver
#endif
{
	//[HideInInspector]
	public string collaborationId;

#if UNITY_EDITOR

	private readonly Color[] colors = {
		new Color(1, 0, 0, 0.5f), new Color(1, 0, 0, 1),
		new Color(0, 1, 0, 0.5f), new Color(0, 1, 0, 1),
		new Color(0, 0, 1, 0.5f), new Color(0, 0, 1, 1),
		new Color(1, 0.827f, 0.835f, 0.5f), new Color(1, 0.827f, 0.835f, 1),
		new Color(0.976f, 0, 0.843f, 0.5f), new Color(0.976f, 0, 0.843f, 1),
		new Color(0, 0.823f, 1, 0.5f), new Color(0, 0.823f, 1, 1),
		new Color(0.929f, 0.906f, 0.012f, 0.5f), new Color(0.929f, 0.906f, 0.012f, 1),
		new Color(0, 0.525f, 0.27f, 0.5f), new Color(0, 0.525f, 0.27f, 1),
		new Color(0.945f, 0.537f, 0.006f, 0.5f), new Color(0.945f, 0.537f, 0.006f, 1),
		new Color(0.388f, 0.216f, 0.525f, 0.5f), new Color(0.388f, 0.216f, 0.525f, 1)
	};

	[Serializable]
	public class HierarchyItem
	{

		public int instanceId;
		public int siblingIndex;
		public int parentInstanceId;
		public string name;
		public List<int> materialIds;
		public string id;
		// Required to detect transform changes from inspector
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 scale;

		public override bool Equals(object obj)
		{
			return Equals(obj as HierarchyItem);
		}

		public bool Equals(HierarchyItem h)
		{
			return h != null &&
					instanceId == h.instanceId &&
					siblingIndex == h.siblingIndex &&
					parentInstanceId == h.parentInstanceId &&
					name == h.name &&
					((materialIds == null && h.materialIds == null) || (materialIds != null && h.materialIds != null && materialIds.SequenceEqual(h.materialIds))) &&
					position == h.position &&
					rotation == h.rotation &&
					scale == h.scale;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				instanceId, siblingIndex, parentInstanceId, name, materialIds, id,
				HashCode.Combine(position, rotation, scale)
			);
		}

	}


	public struct MaterialChange
	{

		public int slot;
		public string materialPath;

	}


	[Serializable]
	private struct ParticipantInfo
	{

		public string id;
		public string displayName;

	};

	public event Action<Transform> OnTransformAdded;
	public event Action<Transform, Vector3?, Quaternion?, Vector3?> OnTransformChanged;
	public event Action<Transform, string> OnNameChanged;
	public event Action<string, string, string, string> OnTransformRemoved;
	public event Action<Transform, List<int>> OnMaterialsChanged;
	public event Action<Transform, Transform> OnTransformReparented;

	public ISettingsAccessor settingsAccessor;
	public Dictionary<string, string> otherParticipantsDisplayNames = new();
	public Dictionary<string, List<Transform>> otherParticipantSelections = new();

	//[SerializeField] private List<HierarchyItem> hierarchy;
	public List<HierarchyItem> hierarchy;
	[SerializeField] private List<int> serializedDictionary;
	private HashSet<int> dictionary;
	[SerializeField] private Dictionary<string, ParticipantInfo> sidToParticipant = new();

	private SerializedTree tree;
	private float time;

	private readonly TransformVisitor visitor = new();

	public void SetTree(SerializedTree tree)
	{
		this.tree = tree;
		ResetHierarchy();
	}

	public void OnBeforeSerialize()
	{
		// Unity can't serialize HashSets, so we're improvising here

		serializedDictionary = new List<int>();
		foreach (int id in dictionary)
		{
			serializedDictionary.Add(id);
		}
	}

	public void OnAfterDeserialize()
	{
		// Unity can't serialize HashSets, so we're improvising here

		dictionary = new HashSet<int>();
		if (serializedDictionary != null)
		{
			foreach (int id in serializedDictionary)
			{
				dictionary.Add(id);
			}
		}
	}

	private void BuildHierarchy(List<HierarchyItem> targetHierarchy, HashSet<int> targetDictionary)
	{
		if (tree == null)
		{
			return;
		}

		long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		visitor.VisitAll(transform, (transform) => {
			// Basic hierarchy item is same for all transforms
			tree.ids.TryGetValue(transform.gameObject, out string id);
			HierarchyItem hierarchyItem = new()
			{
				instanceId = transform.GetInstanceID(),
				siblingIndex = transform.GetSiblingIndex(),
				parentInstanceId = transform.parent == null ? 0 : transform.parent.GetInstanceID(),
				name = transform.name,
				id = id,
				position = transform.localPosition,
				rotation = transform.localRotation,
				scale = transform.localScale
			};
			// Add material information if this is a qualified GO
			if (PrefabUtility.IsPartOfAnyPrefab(transform.gameObject))
			{
				// This should work for MeshRenderer and SkinnedMeshRenderer alike
				if (transform.gameObject.TryGetComponent<Renderer>(out var renderer))
				{
					hierarchyItem.materialIds = new List<int>(renderer.sharedMaterials.Length);
					foreach (Material m in renderer.sharedMaterials)
					{
						hierarchyItem.materialIds.Add(m.GetInstanceID());
					}
				}
			}
			targetHierarchy.Add(hierarchyItem);
			targetDictionary.Add(transform.GetInstanceID());
		});

		long end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		DebugUtils.Log(DebugContext.Collaboration, $"BuildHierarchy needed {end - start}ms");
	}

	public void ResetHierarchy()
	{
		// Build the initial hierarchy when the marker is created

		if (hierarchy == null)
		{
			hierarchy = new();
		}
		else
		{
			hierarchy.Clear();
		}
		if (dictionary == null)
		{
			dictionary = new();
		}
		else
		{
			dictionary.Clear();
		}

		BuildHierarchy(hierarchy, dictionary);
	}

	void Awake()
	{
		ResetHierarchy();
	}

	private (string parentId, string path) CreatePath(HierarchyItem item, List<HierarchyItem> hierarchy)
	{
		// Walk the hierarchy back up to this root object
		string result = "";
		int instanceId = transform.GetInstanceID();
		while (item.instanceId != instanceId && item.id == null)
		{
			result = NameEncoder.Encode(item.name, item.siblingIndex) + result;
			item = hierarchy.FirstOrDefault(candidate => candidate.instanceId == item.parentInstanceId);
		}
		return (item.id, result);
	}

	public void FindHierarchyChanges()
	{
		if (tree == null)
		{
			return;
		}

		List<HierarchyItem> newHierarchy = new();
		HashSet<int> newDictionary = new();

		BuildHierarchy(newHierarchy, newDictionary);

		// Added instance IDs indicate new objects in the collaboration subtree
		HashSet<int> addedInstanceIds = newDictionary.Except(dictionary).ToHashSet();
		if (addedInstanceIds.Count > 0)
		{
			foreach (int id in addedInstanceIds)
			{
				Transform obj = EditorUtility.InstanceIDToObject(id) as Transform;
				// Only look at the object parented to a known object
				if (dictionary.Contains(obj.parent.GetInstanceID()))
				{
					DebugUtils.Log(DebugContext.TransformAddedOutgoing, $"New object {obj.name} added to known parent {obj.parent.name}");

					OnTransformAdded?.Invoke(obj);
				}
			}
		}

		// Removed instance IDs might be either moved out of the collaboration hierarchy, or deleted entirely
		HashSet<int> removedInstanceIds = dictionary.Except(newDictionary).ToHashSet();
		if (removedInstanceIds.Count > 0)
		{
			HashSet<int> ignoreRemovedInstanceIds = new();
			foreach (int id in removedInstanceIds)
			{
				Transform obj = EditorUtility.InstanceIDToObject(id) as Transform;
				if (obj != null)
				{
					// The object still exists, so it was likely moved out of the collaboration object
					// Try to find the parent of the moved subtree
					while (obj.parent != null && removedInstanceIds.Contains(obj.parent.GetInstanceID()))
					{
						ignoreRemovedInstanceIds.Add(obj.GetInstanceID());
						obj = obj.parent;
					}
				}
				else
				{
					// The object no longer exists, so it was likely deleted from the scene
					// Try to find the parent of the deleted subtree
					HierarchyItem item = hierarchy.First(item => item.instanceId == id);
					while (item.parentInstanceId != 0 && removedInstanceIds.Contains(item.parentInstanceId))
					{
						ignoreRemovedInstanceIds.Add(item.instanceId);
						item = hierarchy.First(item => item.instanceId == id);
					}
				}
			}
			HashSet<int> relevantRemovedInstanceIds = removedInstanceIds.Except(ignoreRemovedInstanceIds).ToHashSet();
			foreach (int id in relevantRemovedInstanceIds)
			{
				HierarchyItem item = hierarchy.First(item => item.instanceId == id);
				string parentId = null;
				string path = null;

				if (item.id == null)
				{
					(parentId, path) = CreatePath(item, hierarchy);
					DebugUtils.Log(DebugContext.TransformRemovedOutgoing, $"Object {item.name} with parent ID ${parentId} / path {path} was removed from the hierarchy");
				}
				else
				{
					DebugUtils.Log(DebugContext.TransformRemovedOutgoing, $"Object {item.name} with ID ${item.id} was removed from the hierarchy");
				}

				OnTransformRemoved?.Invoke(item.id, parentId, path, item.name);
			}
		}


		// Other differences indicate changes in the collaboration hierarchy
		if (!newHierarchy.SequenceEqual(hierarchy))
		{
			List<HierarchyItem> differences = newHierarchy.Except(hierarchy).ToList();
			foreach (HierarchyItem item in differences)
			{
				// We need to look at this if it is not contained in the added or removed IDs
				if (!addedInstanceIds.Contains(item.instanceId) && !removedInstanceIds.Contains(item.instanceId))
				{
					HierarchyItem oldItem = hierarchy.First(old => old.instanceId == item.instanceId);
					if (oldItem.name != item.name)
					{
						// Name change
						//Debug.Log("NAME CHANGE: id: " + item.instanceId + " /// name: " + item.name + " /// silbing index: " + item.siblingIndex + " /// parent id: " + item.parentInstanceId);
						Transform obj = EditorUtility.InstanceIDToObject(item.instanceId) as Transform;
						OnNameChanged?.Invoke(obj, item.name);
					}
					if (oldItem.parentInstanceId != item.parentInstanceId)
					{
						// Reparenting
						Transform obj = EditorUtility.InstanceIDToObject(item.instanceId) as Transform;
						Transform oldParent = EditorUtility.InstanceIDToObject(oldItem.parentInstanceId) as Transform;
						OnTransformReparented?.Invoke(obj, oldParent);
					}
					else if (oldItem.siblingIndex != item.siblingIndex)
					{
						// Sibling index change, only relevant for children of this transform
						Transform obj = EditorUtility.InstanceIDToObject(item.instanceId) as Transform;
						if (obj != transform)
						{
							Debug.Log("SIBLING INDEX CHANGE: id: " + item.instanceId + " /// name: " + item.name + " /// silbing index: " + item.siblingIndex + " /// parent id: " + item.parentInstanceId);
						}
					}
					if (oldItem.materialIds != null && item.materialIds != null && !item.materialIds.SequenceEqual(oldItem.materialIds))
					{
						Transform obj = EditorUtility.InstanceIDToObject(item.instanceId) as Transform;
						Material[] currentMaterials = obj.GetComponent<Renderer>().sharedMaterials;
						List<int> slots = new(currentMaterials.Length);
						for (int i = 0; i < oldItem.materialIds.Count; ++i)
						{
							if (oldItem.materialIds[i] != item.materialIds[i])
							{
								slots.Add(i);
							}
						}
						OnMaterialsChanged?.Invoke(obj, slots);
					}
					if (oldItem.position != item.position || oldItem.rotation != item.rotation || oldItem.scale != item.scale)
					{
						Transform obj = EditorUtility.InstanceIDToObject(item.instanceId) as Transform;
						OnTransformChanged?.Invoke(
							obj,
							oldItem.position != item.position ? obj.transform.localPosition : null,
							oldItem.rotation != item.rotation ? obj.transform.localRotation : null,
							oldItem.scale != item.scale ? obj.transform.localScale : null
						);
					}
				}
			}
		}

		// Delta has been processed, record the new state
		hierarchy = newHierarchy;
		dictionary = newDictionary;
	}

	void Update()
	{
		time += Time.deltaTime;

		if (tree != null && settingsAccessor != null)
		{
			if (time > settingsAccessor.GetHierachyCheckDeltaTime())
			{
				time = 0f;
				FindHierarchyChanges();
			}
		}
	}

	void OnDrawGizmos()
	{
		if (settingsAccessor == null || !settingsAccessor.IsShowCollaboratorSelection())
		{
			return;
		}

		int count = 0;
		int max = colors.Length / 2;

		foreach (KeyValuePair<string, List<Transform>> pair in otherParticipantSelections)
		{
			foreach (Transform t in pair.Value)
			{
				Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
				if (renderers.Length > 0)
				{
					Bounds bounds = renderers[0].bounds;
					for (int i = 1; i < renderers.Length; ++i)
					{
						bounds.Encapsulate(renderers[i].bounds.min);
						bounds.Encapsulate(renderers[i].bounds.max);
					}

					string displayName = "Unknown User";
					if (sidToParticipant.TryGetValue(pair.Key, out ParticipantInfo participant))
					{
						displayName = participant.displayName;
					}

					Gizmos.matrix = Matrix4x4.identity;
					Gizmos.color = colors[count % max];
					Gizmos.DrawCube(bounds.center, bounds.extents * 2.01f);
					Gizmos.color = colors[(count % max) + 1];
					Gizmos.DrawWireCube(bounds.center, bounds.extents * 2.01f);
					GUI.color = Color.white;
					Handles.Label(bounds.center, displayName);
				}
			}

			count++;
		}
	}

	public void MapSidToParticipant(string sid, string id, string displayName)
	{
		if (sidToParticipant.TryGetValue(sid, out ParticipantInfo result))
		{
			result.id = id;
			result.displayName = displayName;
		}
		else
		{
			ParticipantInfo participant = new()
			{
				id = id,
				displayName = displayName
			};
			sidToParticipant.Add(sid, participant);
		}
	}

	public void RemoveSid(string sid)
	{
		sidToParticipant.Remove(sid);
		otherParticipantSelections.Remove(sid);
	}

#endif

}

}
