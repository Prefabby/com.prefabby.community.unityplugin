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

using UnityEngine;
using UnityEngine.Assertions;

namespace Prefabby
{

class JsonV1Deserializer : IDeserializer
{

	private readonly IInstanceCreator instanceCreator;
	private readonly ISerializationErrorHandler errorHandler;
	private readonly SerializedTree tree;
	private readonly PrefabDictionary dictionary;
	private readonly Dictionary<string, SerializedGameObject> idToSerializedGameObject = new();

	public JsonV1Deserializer(IInstanceCreator instanceCreator, ISerializationErrorHandler errorHandler, SerializedTree tree, PrefabDictionary dictionary)
	{
		Assert.IsNotNull(instanceCreator);
		Assert.IsNotNull(errorHandler);
		Assert.IsNotNull(tree);
		Assert.IsNotNull(dictionary);

		this.instanceCreator = instanceCreator;
		this.errorHandler = errorHandler;
		this.tree = tree;
		this.dictionary = dictionary;
	}

	public string GetRepresentation()
	{
		return "JsonV1";
	}

	public Transform Deserialize(Transform parent)
	{
		tree.ids ??= new();

		foreach (SerializedGameObject sgo in tree.gameObjects)
		{
			idToSerializedGameObject.Add(sgo.id, sgo);
		}

		return Deserialize(idToSerializedGameObject[tree.root], parent).transform;
	}

	private GameObject Deserialize(SerializedGameObject serializedGameObject, Transform parent)
	{
		DebugUtils.Log(DebugContext.Deserialization, $"Deserializing serialized GO {serializedGameObject.name}");

		GameObject go = null;
		Transform useParent = parent;

		if (serializedGameObject.path != null)
		{
			// The path might point to an existing prefab part which is modified,
			// or to a parent for a newly created prefab
			Transform transformAtPath = CommonUtils.FindWithPath(parent, serializedGameObject.path);
			if (transformAtPath != null && serializedGameObject.prefab == null)
			{
				go = transformAtPath.gameObject;
			}
			else
			{
				useParent = transformAtPath;
			}
		}

		if (go == null)
		{
			if (serializedGameObject.prefab != null)
			{
				PrefabDictionaryItem item = dictionary.GetItemById(serializedGameObject.prefab.id);
				if (item != null)
				{
					if (item.hint != null)
					{
						go = InstantiateFromHint(item.path, serializedGameObject.prefab, item.hint);
					}
					else
					{
						go = instanceCreator.InstantiateGameObjectFromPath(item.path, serializedGameObject.prefab.name, serializedGameObject.prefab.type);
					}
				}

				if (go == null)
				{
					errorHandler.HandleError($"Failed to instantiate serialized game object {serializedGameObject.prefab.name}");
					return null;
				}
			}
			else
			{
				go = new();
			}
		}

		tree.ids.Add(go, serializedGameObject.id);

		if (serializedGameObject.status != null)
		{
			if (serializedGameObject.status == SerializedGameObjectStatus.Active)
			{
				go.SetActive(true);
			}
			else if (serializedGameObject.status == SerializedGameObjectStatus.Inactive)
			{
				go.SetActive(false);
			}
		}
		if (serializedGameObject.name != null)
		{
			go.name = serializedGameObject.name;
		}
		if (serializedGameObject.position != null)
		{
			go.transform.localPosition = serializedGameObject.position.ToVector3();
		}
		if (serializedGameObject.rotation != null)
		{
			go.transform.localRotation = Quaternion.Euler(serializedGameObject.rotation.ToVector3());
		}
		if (serializedGameObject.scale != null)
		{
			go.transform.localScale = serializedGameObject.scale.ToVector3();
		}

		// We may be changing properties of a prefab child, in that case reparenting is not necessary and would cause an error message.
		if (go.transform.parent == null)
		{
			go.transform.SetParent(useParent, false);
		}

		ApplyMaterialChanges(go, serializedGameObject, dictionary);

		if (serializedGameObject.children != null)
		{
			foreach (string childId in serializedGameObject.children)
			{
				Deserialize(idToSerializedGameObject[childId], go.transform);
			}
		}

		return go;
	}

	private GameObject InstantiateFromHint(string path, PrefabReference prefab, ArtPackHint hint)
	{
		Assert.IsNotNull(hint);

		DebugUtils.Log(DebugContext.Deserialization, $"Trying to instantiate {path} /// {prefab.name} /// using key {hint.key}");

		if (ArtPackHelperRepository.TryGetArtPackHelper(hint.key, out IArtPackHelper artPackHelper))
		{
			string fullPath = artPackHelper.DeterminePrefabPath(prefab, hint);
			return instanceCreator.InstantiateGameObjectFromPath(fullPath, prefab.name, prefab.type);
		}
		else
		{
			DebugUtils.LogError(DebugContext.Deserialization, $"Can't find art pack helper for key {hint.key}, plugin may need an update!");
		}

		return null;
	}

	private void ApplyMaterialChanges(GameObject go, SerializedGameObject serializedGameObject, PrefabDictionary prefabDictionary)
	{
		if (go == null || serializedGameObject.materials == null || serializedGameObject.materials.Count == 0)
		{
			return;
		}
		if (!go.TryGetComponent<Renderer>(out var renderer))
		{
			DebugUtils.Log(DebugContext.Deserialization, $"GO {go.name} has no renderer, but there are material changes tracked!");
			return;
		}

		Material[] newMaterials = (Material[]) renderer.sharedMaterials.Clone();
		foreach (MaterialReference materialReference in serializedGameObject.materials)
		{
			newMaterials[materialReference.slot] = FindMaterial(prefabDictionary, materialReference.id, materialReference.name);
		}
		renderer.sharedMaterials = newMaterials;
	}

	public Material FindMaterial(PrefabDictionary prefabDictionary, string id, string name)
	{
		PrefabDictionaryItem item = prefabDictionary.GetItemById(id);
		Assert.IsNotNull(item);

		Material material;
		if (item.hint != null)
		{
			material = InstantiateMaterialFromHint(item.hint, name);
		}
		else
		{
			material = instanceCreator.InstantiateMaterialFromPath(item.path, name);
		}

		return material;
	}

	private Material InstantiateMaterialFromHint(ArtPackHint hint, string name)
	{
		Assert.IsNotNull(hint);
		Assert.IsNotNull(name);

		DebugUtils.Log(DebugContext.Deserialization, $"Trying to find material {name} /// using key {hint.key}");

		if (ArtPackHelperRepository.TryGetArtPackHelper(hint.key, out IArtPackHelper artPackHelper))
		{
			List<string> paths = artPackHelper.DetermineMaterialPaths(name, hint);
			foreach (string option in paths)
			{
				Material result = instanceCreator.InstantiateMaterialFromPath(option, name);
				if (result != null)
				{
					return result;
				}
			}
		}
		else
		{
			DebugUtils.LogError(DebugContext.Deserialization, "Can't find art pack helper for key {hint.key}, plugin may need an update!");
		}

		return null;
	}

}

}
