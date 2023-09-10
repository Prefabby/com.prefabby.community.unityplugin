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
using UnityEditor;
using System;

namespace Prefabby
{

class TwoTransformsVisitor
{
	public void VisitAll(Transform transform1, Transform transform2, Action<Transform, Transform> visitorFn)
	{
		Assert.IsNotNull(transform1);
		Assert.IsNotNull(transform2);
		Assert.IsNotNull(visitorFn);

		VisitAllRecursively(transform1, transform2, visitorFn);
	}

	static void VisitAllRecursively(Transform transform1, Transform transform2, Action<Transform, Transform> visitorFn)
	{
		visitorFn(transform1, transform2);

		// The scene instance may have more children but that's not relevant for comparison with the prefab.
		for (int i = 0, max = Mathf.Min(transform1.childCount, transform2.childCount); i < max; ++i)
		{
			VisitAllRecursively(transform1.GetChild(i), transform2.GetChild(i), visitorFn);
		}
	}
}

class JsonV1Serializer : ISerializer
{
	private readonly SerializedTree tree;
	private readonly GameObject root;
	private readonly PrefabDictionary dictionary;
	private readonly TwoTransformsVisitor twoTransformsVisitor = new();

	public JsonV1Serializer(GameObject root, PrefabDictionary dictionary)
	{
		this.root = root;
		this.dictionary = dictionary;

		tree = new()
		{
			gameObjects = new(),
			ids = new()
		};
	}

	public string GetRepresentation()
	{
		return "JsonV1";
	}

	public SerializedTree Serialize()
	{
		SerializedGameObject rootSerializedGo = SerializeInternal(root, null, null);
		tree.root = rootSerializedGo.id;
		return tree;
	}

	private string GetOrCreateGameObjectId(GameObject go)
	{
		if (!tree.ids.TryGetValue(go, out string id))
		{
			id = Guid.NewGuid().ToString("N");
			tree.ids.Add(go, id);
		}
		return id;
	}

	private SerializedGameObject InitializeSerializedGO(GameObject go, bool isPrefab)
	{
		SerializedGameObject result = new()
		{
			id = GetOrCreateGameObjectId(go),
			name = go.name,
			siblingIndex = go.transform.GetSiblingIndex()
		};

		if (isPrefab)
		{
			(string path, string name) = EditorUtils.GetPathAndNameFromPrefab(go);
			PrefabDictionaryItem item = dictionary.Resolve(path, name);

			if (item != null)
			{
				GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
				result.prefab = new PrefabReference
				{
					id = item.id,
					name = prefab.name,
					type = MapPrefabAssetType(go)
				};

				result.materials = EditorUtils.FindMaterialChanges(go, prefab, dictionary);

				if (ArtPackHelperRepository.TryGetArtPackHelper(item.hint.key, out IArtPackHelper artPackHelper))
				{
					result.prefab.hint = artPackHelper.CreatePrefabHint(go, result, path, name, prefab, item);
				}
			}
		}
		else
		{
			if (go.transform.localPosition != Vector3.zero)
			{
				result.position = new SerializedVector(go.transform.localPosition);
			}
			if (go.transform.localRotation != Quaternion.identity)
			{
				result.rotation = new SerializedVector(go.transform.localEulerAngles);
			}
			if (go.transform.localScale != Vector3.one)
			{
				result.scale = new SerializedVector(go.transform.localScale);
			}
		}

		return result;
	}

	private GameObject FindChildWithName(GameObject go, string name, Stack<int> stack)
	{
		GameObject result = null;
		if (go.name == name)
		{
			result = go;
		}
		else
		{
			for (int i = 0, max = go.transform.childCount; i < max; ++i)
			{
				result = FindChildWithName(go.transform.GetChild(i).gameObject, name, stack);
				if (result != null)
				{
					stack.Push(i);
					break;
				}
			}
		}
		return result;
	}

	private void FindPrefabChanges(SerializedGameObject serializedGo, GameObject go)
	{
		GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);

		twoTransformsVisitor.VisitAll(prefab.transform, go.transform, (Transform prefabTransform, Transform sceneTransform) => {
			List<MaterialReference> materialReferences = null;
			if (prefabTransform.gameObject.activeSelf != sceneTransform.gameObject.activeSelf ||
				prefabTransform.name != sceneTransform.name ||
				prefabTransform.localPosition != sceneTransform.localPosition ||
				prefabTransform.localRotation != sceneTransform.localRotation ||
				prefabTransform.localScale != sceneTransform.localScale ||
				(materialReferences = EditorUtils.FindMaterialChanges(sceneTransform.gameObject, prefabTransform.gameObject, dictionary)) != null
			)
			{
				SerializedGameObject result;
				if (tree.ids.TryGetValue(sceneTransform.gameObject, out string id))
				{
					result = tree.FindById(id);
				}
				else
				{
					result = new()
					{
						id = GetOrCreateGameObjectId(sceneTransform.gameObject),
					};

					serializedGo.children ??= new();
					serializedGo.children.Add(result.id);

					tree.gameObjects.Add(result);
				}
				if (prefabTransform.gameObject.activeSelf != sceneTransform.gameObject.activeSelf)
				{
					result.status = sceneTransform.gameObject.activeSelf ? SerializedGameObjectStatus.Active : SerializedGameObjectStatus.Inactive;
				}
				if (prefabTransform.name != sceneTransform.name)
				{
					result.name = sceneTransform.name;
					// We need to use the path from the original prefab, because if the name is changed, too, we won't find it right after instantiation.
					result.path = EditorUtils.CreatePath(prefabTransform, prefab.transform);
				}
				if (prefabTransform.localPosition != sceneTransform.localPosition)
				{
					result.position = new SerializedVector(sceneTransform.localPosition);
				}
				if (prefabTransform.localRotation != sceneTransform.localRotation)
				{
					result.rotation = new SerializedVector(sceneTransform.localEulerAngles);
				}
				if (prefabTransform.localScale != sceneTransform.localScale)
				{
					result.scale = new SerializedVector(sceneTransform.localScale);
				}
				if (materialReferences != null)
				{
					result.materials = materialReferences;
				}
			}
		});
	}

	private SerializedGameObject SerializeInternal(GameObject go, SerializedGameObject parent, GameObject lastPrefabRoot)
	{
		SerializedGameObject serializedGo = null;

		if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
		{
			serializedGo = InitializeSerializedGO(go, true);
			if (parent != null)
			{
				parent.children ??= new();
				parent.children.Add(serializedGo.id);
			}
			if (lastPrefabRoot != null)
			{
				serializedGo.path = EditorUtils.CreatePath(go.transform.parent, lastPrefabRoot.transform);
			}

			tree.gameObjects.Add(serializedGo);

			lastPrefabRoot = go;

			FindPrefabChanges(serializedGo, go);
		}
		else if (PrefabUtility.IsPartOfAnyPrefab(go))
		{
			// Ignore, these GOs were covered above already
		}
		else
		{
			serializedGo = InitializeSerializedGO(go, false);
			if (parent != null)
			{
				parent.children ??= new();
				parent.children.Add(serializedGo.id);
			}
			tree.gameObjects.Add(serializedGo);
			lastPrefabRoot = null;
		}

		foreach (Transform child in go.transform)
		{
			SerializeInternal(child.gameObject, serializedGo ?? parent, lastPrefabRoot);
		}

		return serializedGo;
	}

	private string MapPrefabAssetType(GameObject go)
	{
		PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(go);
		return prefabAssetType switch
		{
			PrefabAssetType.Model => "Model",
			PrefabAssetType.Regular => "Prefab",
			PrefabAssetType.Variant => "Variant",
			_ => null,
		};
	}

}

}
