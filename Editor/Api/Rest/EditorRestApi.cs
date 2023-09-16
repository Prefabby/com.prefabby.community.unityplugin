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
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;
using Newtonsoft.Json;

namespace Prefabby
{

class EditorRestApi : CommonRestApi
{

	private readonly object owner;
	private readonly ISettingsAccessor settingsAccessor;

	public EditorRestApi(object owner, ISettingsAccessor settingsAccessor)
	{
		this.owner = owner;
		this.settingsAccessor = settingsAccessor;

		DebugUtils.Log(DebugContext.RestApi, $"Initializing REST API with host {settingsAccessor.GetApiHost()}");
	}

	public void GetUserDetails(string accessKey, Action<MyUserInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			GetUserDetailsCoroutine(settingsAccessor.GetApiHost(), accessKey, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator GetUserDetailsCoroutine(string apiHost, string accessKey, Action<MyUserInfo> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get($"{apiHost}/api/v1/users/me");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "GetUserDetailsCoroutine succeeded!");

			GetMyUserResponse response = JsonConvert.DeserializeObject<GetMyUserResponse>(request.downloadHandler.text);
			MyUserInfo myUserInfo = response.data;

			successCallback(myUserInfo);
		}
		else
		{
			errorCallback();
		}
	}

	public void GetStorefrontAssets(string query, Action<GetStorefrontAssetsResponse> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			GetStorefrontAssetsCoroutine(Region.DeriveStorefrontApiHost(settingsAccessor.GetApiHost()), query, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator GetStorefrontAssetsCoroutine(string apiHost, string query, Action<GetStorefrontAssetsResponse> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get($"{apiHost}/api/v1/storefront/assets?search=" + UnityWebRequest.EscapeURL(query));
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, $"GetStorefrontAssets result: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "GetStorefrontAssetsCoroutine succeeded!");

			successCallback(JsonConvert.DeserializeObject<GetStorefrontAssetsResponse>(request.downloadHandler.text));
		}
		else
		{
			errorCallback();
		}
	}

	public void GetImage(string url, Action<Texture2D> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			GetImageCoroutine(url, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator GetImageCoroutine(string url, Action<Texture2D> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get(url);
		DownloadHandlerTexture downloadHandlerTexture = new DownloadHandlerTexture(true);
		request.downloadHandler = downloadHandlerTexture;
		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "GetImageCoroutine succeeded!");

			successCallback(downloadHandlerTexture.texture);
		}
		else
		{
			errorCallback();
		}
	}

	public void GetAsset(string assetId, bool count, Action<AssetInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			GetAssetCoroutine(Region.DeriveStorefrontApiHost(settingsAccessor.GetApiHost()), assetId, count, successCallback, errorCallback),
			owner
		);
	}

	public void PublishAsset(string accessKey, CreateAssetRequest request, Action<AssetInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			CreateOrUpdateAssetCoroutine(settingsAccessor.GetApiHost(), accessKey, null, request, successCallback, errorCallback),
			owner
		);
	}

	public void UpdateAsset(string accessKey, string assetId, CreateAssetRequest request, Action<AssetInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			CreateOrUpdateAssetCoroutine(settingsAccessor.GetApiHost(), accessKey, assetId, request, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator CreateOrUpdateAssetCoroutine(string apiHost, string accessKey, string assetId, CreateAssetRequest createAssetRequest, Action<AssetInfo> successCallback, Action errorCallback)
	{
		string json = JsonConvert.SerializeObject(createAssetRequest, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

		string path = assetId == null ? $"{apiHost}/api/v1/assets" : $"{apiHost}/api/v1/assets/{assetId}";
		string method = assetId == null ? "POST" : "PUT";
		using UnityWebRequest request = new(path, method);
		request.uploadHandler = new UploadHandlerRaw(jsonBytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, $"CreateOrUpdateAssetCoroutine result: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			AssetResponse response = JsonConvert.DeserializeObject<AssetResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			errorCallback();
		}
	}

	public void ListCollaborations(string accessKey, Action<List<CollaborationShortInfo>> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			ListCollaborationsCoroutine(settingsAccessor.GetApiHost(), accessKey, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator ListCollaborationsCoroutine(string apiHost, string accessKey, Action<List<CollaborationShortInfo>> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get($"{apiHost}/api/v1/collaboration");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.Collaboration, $"ListCollaborationsCoroutine result: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "ListCollaborationsCoroutine succeeded!");

			ListCollaborationsResponse response = JsonConvert.DeserializeObject<ListCollaborationsResponse>(request.downloadHandler.text);
			List<CollaborationShortInfo> collaborationShortInfoList = response.data;

			successCallback(collaborationShortInfoList);
		}
		else
		{
			errorCallback();
		}
	}

	public void StartCollaboration(string accessKey, CreateCollaborationRequest createCollaborationRequest, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		DebugUtils.Log(DebugContext.Collaboration, $"Calling start collaboration coroutine with apiHost: {settingsAccessor.GetApiHost()}");

		EditorCoroutineUtility.StartCoroutine(
			StartCollaborationCoroutine(settingsAccessor.GetApiHost(), accessKey, createCollaborationRequest, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator StartCollaborationCoroutine(string apiHost, string accessKey, CreateCollaborationRequest createCollaborationRequest, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		DebugUtils.Log(DebugContext.Collaboration, "Calling start collaboration...");

		string json = JsonConvert.SerializeObject(createCollaborationRequest, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

		// https://forum.unity.com/threads/posting-raw-json-into-unitywebrequest.397871/#post-8511275
		using UnityWebRequest request = new UnityWebRequest($"{apiHost}/api/v1/collaboration", "POST");
		request.uploadHandler = new UploadHandlerRaw(jsonBytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, $"Start collaboration response: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			CreateCollaborationResponse response = JsonConvert.DeserializeObject<CreateCollaborationResponse>(request.downloadHandler.text);
			DebugUtils.Log(DebugContext.Collaboration, $"Start collaboration API successful, collaboration ID: {response.data.id}");
			successCallback(response.data);
		}
		else
		{
			errorCallback();
		}
	}

	public void GetCollaboration(string accessKey, string collaborationId, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			GetCollaborationCoroutine(settingsAccessor.GetApiHost(), accessKey, collaborationId, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator GetCollaborationCoroutine(string apiHost, string accessKey, string collaborationId, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		using UnityWebRequest request = UnityWebRequest.Get($"{apiHost}/api/v1/collaboration/{collaborationId}");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, $"GetCollaboration({collaborationId}) response: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			DebugUtils.Log(DebugContext.RestApi, "GetCollaborationCoroutine succeeded!");

			GetCollaborationResponse response = JsonConvert.DeserializeObject<GetCollaborationResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			errorCallback();
		}
	}

	public void JoinCollaboration(string accessKey, string collaborationId, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			JoinCollaborationCoroutine(settingsAccessor.GetApiHost(), accessKey, collaborationId, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator JoinCollaborationCoroutine(string apiHost, string accessKey, string collaborationId, Action<CollaborationInfo> successCallback, Action errorCallback)
	{
		DebugUtils.Log(DebugContext.Collaboration, $"Calling join collaboration {collaborationId}...");

		using UnityWebRequest request = new UnityWebRequest($"{apiHost}/api/v1/collaboration/{collaborationId}/join", "POST");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		request.downloadHandler = new DownloadHandlerBuffer();
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.Collaboration, $"JoinCollaborationCoroutine({collaborationId}) result: {request.downloadHandler.text}");

		if (request.result == UnityWebRequest.Result.Success)
		{
			JoinCollaborationResponse response = JsonConvert.DeserializeObject<JoinCollaborationResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			Debug.Log(request.error);

			errorCallback();
		}
	}

	public void IdentifyPrefabsForCollaboration(string accessKey, string collaborationId, IdentifyPrefabsRequest identifyPrefabsRequest, Action<List<PrefabDictionaryItem>> successCallback, Action<ErrorInfo> errorCallback)
	{
		EditorCoroutineUtility.StartCoroutine(
			IdentifyPrefabsForCollaborationCoroutine(settingsAccessor.GetApiHost(), accessKey, collaborationId, identifyPrefabsRequest, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator IdentifyPrefabsForCollaborationCoroutine(string apiHost, string accessKey, string collaborationId, IdentifyPrefabsRequest identifyPrefabsRequest, Action<List<PrefabDictionaryItem>> successCallback, Action<ErrorInfo> errorCallback)
	{
		string json = JsonConvert.SerializeObject(identifyPrefabsRequest, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

		using UnityWebRequest request = new UnityWebRequest($"{apiHost}/api/v1/collaboration/{collaborationId}/identify", "POST");
		request.uploadHandler = new UploadHandlerRaw(jsonBytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.PrefabIdentification, "IdentifyPrefabsForCollaboration response is: " + request.downloadHandler.text);

		if (request.result == UnityWebRequest.Result.Success)
		{
			IdentifyPrefabsResponse response = JsonConvert.DeserializeObject<IdentifyPrefabsResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			ApiResponse<dynamic> error = JsonConvert.DeserializeObject<ApiResponse<dynamic>>(request.downloadHandler.text);
			errorCallback(error.error);
		}
	}

	public void IdentifyPrefabsForPublishing(string accessKey, IdentifyPrefabsRequest identifyPrefabsRequest, Action<List<PrefabDictionaryItem>> successCallback, Action<ErrorInfo> errorCallback)
	{
		DebugUtils.Log(DebugContext.Publishing, $"Calling identify prefabs for publishing...");

		EditorCoroutineUtility.StartCoroutine(
			IdentifyPrefabsForPublishingCoroutine(settingsAccessor.GetApiHost(), accessKey, identifyPrefabsRequest, successCallback, errorCallback),
			owner
		);
	}

	private IEnumerator IdentifyPrefabsForPublishingCoroutine(string apiHost, string accessKey, IdentifyPrefabsRequest identifyPrefabsRequest, Action<List<PrefabDictionaryItem>> successCallback, Action<ErrorInfo> errorCallback)
	{
		string json = JsonConvert.SerializeObject(identifyPrefabsRequest, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

		using UnityWebRequest request = new($"{apiHost}/api/v1/prefabidentification", "POST");
		request.uploadHandler = new UploadHandlerRaw(jsonBytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Authorization", $"Bearer {accessKey}");
		yield return request.SendWebRequest();

		DebugUtils.Log(DebugContext.RestApi, "IdentifyPrefabsForPublishing response is: " + request.downloadHandler.text);

		if (request.result == UnityWebRequest.Result.Success)
		{
			IdentifyPrefabsResponse response = JsonConvert.DeserializeObject<IdentifyPrefabsResponse>(request.downloadHandler.text);
			successCallback(response.data);
		}
		else
		{
			ApiResponse<dynamic> error = JsonConvert.DeserializeObject<ApiResponse<dynamic>>(request.downloadHandler.text);
			errorCallback(error.error);
		}
	}

}

}
