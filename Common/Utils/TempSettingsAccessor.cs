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

namespace Prefabby
{

class TempSettingsAccessor : ISettingsAccessor
{

	private readonly string derivedApiHost;

	public TempSettingsAccessor(string derivedApiHost)
	{
		this.derivedApiHost = derivedApiHost;
	}

	public string GetApiHost()
	{
		return derivedApiHost;
	}

	public float GetHierachyCheckDeltaTime()
	{
		throw new NotImplementedException();
	}

	public string GetUserId()
	{
		throw new NotImplementedException();
	}

	public bool IsShowCollaboratorSelection()
	{
		throw new NotImplementedException();
	}

}

}