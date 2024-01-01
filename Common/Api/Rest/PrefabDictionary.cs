/*
	Prefabby Unity plugin
    Copyright (C) 2023-2024  Matthias Gall <matt@prefabby.com>

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

namespace Prefabby
{

[System.Serializable]
class PrefabDictionary
{

	public List<PrefabDictionaryItem> items = new();

	public PrefabDictionaryItem Resolve(string path, string name)
	{
		DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, $"Trying to resolve path={path}, name={name}...");

		// First, try resolving by hints
		foreach (PrefabDictionaryItem item in items)
		{
			if (item.hint != null)
			{
				DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, $"- Looking at hint path {item.hint.path}...");
				if (path.Contains(item.hint.path))
				{
					DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, "- Match found!");
					return item;
				}
			}
		}

		// Very simple resolve procedure
		foreach (PrefabDictionaryItem item in items)
		{
			DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, $"- Looking at known item path {item.path}...");
			if (item.path == path)
			{
				DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, "- Match found!");
				return item;
			}
		}

		DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, "- No match found!");
		return null;
	}

	public void Add(PrefabDictionaryItem item)
	{
		items.Add(item);
	}

	public PrefabDictionaryItem GetItemById(string id)
	{
		DebugUtils.Log(DebugContext.PrefabDictionaryIncoming, $"PrefabDictionary.GetItemById({id}): currently {items.Count} items in the list");
		return items.Find(item => item.id == id);
	}

	public override string ToString()
	{
		return $"{base.ToString()}: {items.Count} items";
	}

}

}
