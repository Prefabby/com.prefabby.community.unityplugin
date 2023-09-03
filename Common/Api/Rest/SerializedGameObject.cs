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
using Newtonsoft.Json.Converters;

namespace Prefabby
{

[JsonConverter(typeof(StringEnumConverter))]
public enum SerializedGameObjectStatus
{
	Active,
	Inactive,
	Deleted
}

[System.Serializable]
public class SerializedGameObject
{

	public string id;
	public SerializedGameObjectStatus? status;
	public string name;
	public string path;
	public int? siblingIndex;

	public SerializedVector position;
	public SerializedVector rotation;
	public SerializedVector scale;

	public List<string> children;

	public PrefabReference prefab;
	public List<MaterialReference> materials;

	public void UpdateMaterial(int slot, string id, string name)
	{
		materials ??= new();
		MaterialReference materialReference = materials.Where(materialReference => materialReference.slot == slot).FirstOrDefault();
		if (materialReference == null)
		{
			materialReference = new();
			materials.Add(materialReference);
		}
		materialReference.slot = slot;
		materialReference.id = id;
		materialReference.name = name;
	}

	public void RemoveChild(string childId)
	{
		children?.Remove(childId);
	}

	public void AddChild(string childId)
	{
		children ??= new();
		children.Add(childId);
	}

}

}
