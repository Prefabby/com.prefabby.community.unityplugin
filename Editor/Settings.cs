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

[CreateAssetMenu(menuName = "Prefabby/Settings")]
public class Settings : ScriptableObject, ISettingsAccessor
{

	public string apiHost = "";
	public string accessKey = "";
	public string userId = "";

	public bool showCollaborationLimitationsNotice = true;
	public float hierachyCheckDeltaTime = 0.5f;
	public bool showCollaboratorSelection = true;
	public bool showActivityLog = false;
	public bool createBackups = true;
	public bool forceRefresh = false;
	public bool logging = false;
	public bool checkForUpdate = true;

	#region ISettingsAccessor

	public bool IsValid()
	{
		return !(string.IsNullOrEmpty(apiHost) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(userId));
	}


	public float GetHierachyCheckDeltaTime()
	{
		return hierachyCheckDeltaTime;
	}

	public string GetApiHost()
	{
		return apiHost;
	}

	public string GetUserId()
	{
		return userId;
	}

	public bool IsShowCollaboratorSelection()
	{
		return showCollaboratorSelection;
	}

	#endregion

}

}
