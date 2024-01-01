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

namespace Prefabby
{

public class NameEncoder
{

	public static string Encode(string name, int index)
	{
		return $"/n[{Encode(name)}]i[{index}]";
	}

	public static string Encode(string name)
	{
		return name.Replace("[", "\\[").Replace("]", "\\]");
	}

	public static string Decode(string name)
	{
		return name.Replace("\\[", "[").Replace("\\]", "]");
	}

}

}
