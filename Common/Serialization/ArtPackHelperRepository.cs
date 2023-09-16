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

using UnityEngine.Assertions;

namespace Prefabby
{

class ArtPackHelperRepository
{

	private static readonly Dictionary<string, IArtPackHelper> artPackHelpers = new()
	{
		{ "SyntyStudios", new SyntyStudioArtPackHelper() },
		{ "polyperfect", new PolyperfectArtPackHelper() }
	};

	public static bool TryGetArtPackHelper(string key, out IArtPackHelper artPackHelper)
	{
		Assert.IsNotNull(key);

		string[] parts = key.Split("/");
		Assert.AreEqual(2, parts.Length);

		return artPackHelpers.TryGetValue(parts[0], out artPackHelper);
	}

}

}
