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

using System;

using UnityEngine;
using UnityEngine.Assertions;

namespace Prefabby
{

class CommonUtils
{

	public static void LogError(string message)
	{
		Debug.LogError($"{message}; please report this error to matt@prefabby.com");
	}

	public static Transform FindWithPath(Transform root, string path)
	{
		Transform node = root;
		string[] nodeSplits = path.Split("/n[", StringSplitOptions.RemoveEmptyEntries);
		foreach (string nodeSplit in nodeSplits)
		{
			string[] nameSplits = nodeSplit.Split("]i[", StringSplitOptions.RemoveEmptyEntries);
			string indexAsString = nameSplits[1][0..(nameSplits[1].Length - 1)];
			int index = int.Parse(indexAsString);
			Assert.IsTrue(index >= 0 && index < node.childCount);
			Transform child = node.GetChild(index);
			Assert.AreEqual(NameEncoder.Decode(nameSplits[0]), child.name, "unexpected child at index");
			node = child;
		}
		Assert.IsNotNull(node, $"Expected to find a node at path {path}, result should not be null!");
		return node;
	}

}

}
