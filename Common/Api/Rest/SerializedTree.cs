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
using Newtonsoft.Json;

namespace Prefabby
{

[System.Serializable]
public class GameObjectKeyDictionary : SerializableDictionary<GameObject, string>
{
}

[System.Serializable]
public class SerializedTree
{

	public string root;
	public List<SerializedGameObject> gameObjects;

	[JsonIgnore]
	public GameObjectKeyDictionary ids = new();

	public SerializedGameObject FindById(string id)
	{
		return gameObjects.Where(sgo => sgo.id == id).FirstOrDefault();
	}

	public SerializedGameObject FindParentOf(string id)
	{
		return gameObjects.Where(sgo => sgo.children != null && sgo.children.Contains(id)).FirstOrDefault();
	}

	public GameObject FindGameObjectById(string id)
	{
		return ids.Where(kvp => kvp.Value == id).FirstOrDefault().Key;
	}

	public (SerializedGameObject parentSGO, GameObject parentGO) FindClosestParent(GameObject go)
	{
		do
		{
			if (ids.TryGetValue(go, out string id))
			{
				return (FindById(id), go);
			}

			go = go.transform.parent?.gameObject;
		}
		while (go != null);

		return (null, null);
	}

	public void RemoveById(string id)
	{
		SerializedGameObject sgo = FindById(id);
		if (sgo != null)
		{
			// First remove children
			if (sgo.children != null)
			{
				foreach (string childId in sgo.children)
				{
					RemoveById(childId);
				}
			}

			// Remove GO->ID mapping
			foreach (var kvp in ids.Where(kvp => kvp.Value == id).ToList())
			{
				ids.Remove(kvp.Key);
			}

			// Remove from SGO list
			gameObjects.Remove(sgo);

			// Remove reference from any parents
			foreach (SerializedGameObject parentSGO in gameObjects)
			{
				parentSGO.children?.Remove(id);
			}
		}
	}

}

}
