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

using UnityEngine;

namespace Prefabby
{

class Region
{

	public static string[] DeriveRegionOptions()
	{
		if (Constants.local)
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Local" };
		}
		else if (Constants.devel)
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Devel" };
		}
		else
		{
			#pragma warning disable CS0162
			return new string[]{ "(please select)", "Falkenstein (Germany)", "Sydney (Australia)" };
		}
	}

	public static int DeriveRegion(string apiHost)
	{
		string[] options = DeriveRegionOptions();
		for (int i = 0; i < options.Length; ++i)
		{
			string test = DeriveApiHost(i);
			if (test == apiHost)
			{
				return i;
			}
		}
		Debug.LogError($"Unable to derive region from host {apiHost}!");
		return 0;
	}

	public static string DeriveApiHost(int region)
	{
		if (Constants.local)
		{
			#pragma warning disable CS0162
			if (region == 1)
			{
				return "http://localhost:8080";
			}
			else
			{
				return "";
			}
		}
		else if (Constants.devel)
		{
			#pragma warning disable CS0162
			if (region == 1)
			{
				return "https://devel.prefabby.com";
			}
			else
			{
				return "";
			}
		}
		else
		{
			#pragma warning disable CS0162
			switch (region)
			{
				case 0:
					return "";
				case 1:
					return "https://fsn.app.prefabby.com";
				case 2:
					return "https://syd.app.prefabby.com";
				default:
					throw new System.Exception("Unsupported region!");
			}
		}
	}

	public static string DeriveStorefrontApiHost(string apiHost)
	{
		if (string.IsNullOrEmpty(apiHost) || apiHost.EndsWith(".app.prefabby.com"))
		{
			return "https://prefabby.com";
		}
		else
		{
			return apiHost;
		}
	}

}

}
