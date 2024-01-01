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
using System.Collections;

using UnityEngine.Networking;
using Newtonsoft.Json;

namespace Prefabby
{

class CommonRestApi
{

	protected IEnumerator GetAssetCoroutine(string apiHost, string assetId, bool count, Action<AssetInfo> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get($"{apiHost}/api/v1/assets/{assetId}?count={JsonConvert.ToString(count)}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, $"GetAssetCoroutine response: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "GetAssetCoroutine succeeded!");

			AssetResponse response = JsonConvert.DeserializeObject<AssetResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			errorCallback();
		}
	}

}

}
