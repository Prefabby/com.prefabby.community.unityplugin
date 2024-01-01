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
using System.Collections.Generic;

using UnityEngine.Assertions;
using Newtonsoft.Json;

using NativeWebSocket;

namespace Prefabby
{

class StompApi
{

	private readonly Dictionary<Type, string> messageTypeToPath = new()
	{
		{ typeof(SelectionChangedMessage), "/selectionChanged" },
		{ typeof(TransformsChangedMessage), "/transformsChanged" },
		{ typeof(TransformAddedMessage), "/transformAdded" },
		{ typeof(TransformRemovedMessage), "/transformRemoved" },
		{ typeof(TransformReparentedMessage), "/transformReparented" },
		{ typeof(MaterialsChangedMessage), "/materialsChanged" }
	};


	private WebSocket websocket;
	private readonly StompFrameSerializer serializer = new();
	private readonly Logger logger;
	private readonly string apiHost;
	private readonly string accessKey;
	private readonly string collaborationId;
	private string sid = null;
	private long sequence = 0;

	public event Action<string> OnError;
	public event Action<SyncMessage> OnSync;
	public event Action<TransformsChangedMessage> OnTransformsChanged;
	public event Action<SelectionChangedMessage> OnSelectionChanged;
	public event Action<ConnectMessage> OnConnect;
	public event Action<BaseMessage> OnDisconnect;
	public event Action<PrefabDictionaryItemsAddedMessage> OnPrefabDictionaryItemsAdded;
	public event Action<TransformAddedMessage> OnTransformAdded;
	public event Action<TransformRemovedMessage> OnTransformRemoved;
	public event Action<TransformReparentedMessage> OnTransformReparented;
	public event Action<MaterialsChangedMessage> OnMaterialsChanged;

	public StompApi(Logger logger, string apiHost, string accessKey, string collaborationId)
	{
		this.logger = logger;
		this.apiHost = apiHost;
		this.accessKey = accessKey;
		this.collaborationId = collaborationId;
	}

	public async void Connect()
	{
		string protocol = apiHost[0..apiHost.IndexOf("://")].Replace("https", "wss").Replace("http", "ws");
		string host = apiHost[(apiHost.IndexOf("://") + 3)..^0];
		DebugUtils.Log(DebugContext.StompApi, $"Connecting to WebSocket host {protocol}://{host}");

		websocket = new WebSocket($"{protocol}://{host}/api/collaboration/websocket");

		websocket.OnError += (string message) =>
		{
			DebugUtils.Log(DebugContext.StompApi, $"WebSocket error: {message}");

			logger.Append($"An error occurred: {message}");

			OnError?.Invoke(message);
		};

		websocket.OnOpen += async () =>
		{
			var connect = new StompFrame(StompCommand.CONNECT);
			connect["accept-version"] = "1.2";
			connect["host"] = "localhost";
			connect["login"] = accessKey;
			await websocket.SendText(serializer.Serialize(connect));

			logger.Append("CONNECT frame sent");

			// subscribe
			var subscribe = new StompFrame(StompCommand.SUBSCRIBE);
			subscribe["ack"] = "auto";
			subscribe["id"] = collaborationId;
			subscribe["destination"] = "/topic/collaboration/" + collaborationId;
			await websocket.SendText(serializer.Serialize(subscribe));

			logger.Append("SUBSCRIBE frame sent");
		};

		websocket.OnMessage += (bytes) =>
		{
			var text = System.Text.Encoding.UTF8.GetString(bytes);
			StompFrame frame = serializer.Deserialize(text);

			logger.Append("OnMessage: " + text);

			if (frame.Command == StompCommand.MESSAGE)
			{
				// Extract base parameters and skip messages originating from this user
				BaseMessage baseMessage = JsonConvert.DeserializeObject<BaseMessage>(frame.Body);
				if (baseMessage.sid != null && this.sid != null && this.sid.Equals(baseMessage.sid))
				{
					logger.Append($"Ignoring message of type {baseMessage.type}");
					return;
				}

				logger.Append($"Processing message of type {baseMessage.type}...");
				switch (baseMessage.type)
				{
					case "Handshake":
						DebugUtils.Log(DebugContext.StompApi, $"Received handshake message with SID {baseMessage.sid}");
						this.sid = baseMessage.sid;
						break;

					case "Connect":
						ConnectMessage connectMessage = JsonConvert.DeserializeObject<ConnectMessage>(frame.Body);
						OnConnect?.Invoke(connectMessage);
						break;

					case "Disconnect":
						OnDisconnect?.Invoke(baseMessage);
						break;

					case "Sync":
						SyncMessage syncMessage = JsonConvert.DeserializeObject<SyncMessage>(frame.Body);
						OnSync?.Invoke(syncMessage);
						break;

					case "TransformsChanged":
						if (OnTransformsChanged != null)
						{
							TransformsChangedMessage transformsChangedMessage = JsonConvert.DeserializeObject<TransformsChangedMessage>(frame.Body);
							OnTransformsChanged(transformsChangedMessage);
						}
						break;

					case "SelectionChanged":
						if (OnSelectionChanged != null)
						{
							SelectionChangedMessage selectionChangedMessage = JsonConvert.DeserializeObject<SelectionChangedMessage>(frame.Body);
							OnSelectionChanged(selectionChangedMessage);
						}
						break;

					case "PrefabDictionaryItemAdded":
						if (OnPrefabDictionaryItemsAdded != null)
						{
							PrefabDictionaryItemsAddedMessage prefabDictionaryItemsAddedMessage = JsonConvert.DeserializeObject<PrefabDictionaryItemsAddedMessage>(frame.Body);
							OnPrefabDictionaryItemsAdded(prefabDictionaryItemsAddedMessage);
						}
						break;

					case "TransformAdded":
						if (OnTransformAdded != null)
						{
							TransformAddedMessage transformAddedMessage = JsonConvert.DeserializeObject<TransformAddedMessage>(frame.Body);
							OnTransformAdded(transformAddedMessage);
						}
						break;

					case "TransformRemoved":
						if (OnTransformRemoved != null)
						{
							TransformRemovedMessage transformRemovedMessage = JsonConvert.DeserializeObject<TransformRemovedMessage>(frame.Body);
							OnTransformRemoved(transformRemovedMessage);
						}
						break;

					case "TransformReparented":
						if (OnTransformReparented != null)
						{
							TransformReparentedMessage transformReparentedMessage = JsonConvert.DeserializeObject<TransformReparentedMessage>(frame.Body);
							OnTransformReparented(transformReparentedMessage);
						}
						break;

					case "MaterialsChanged":
						if (OnMaterialsChanged != null)
						{
							MaterialsChangedMessage materialsChangedMessage = JsonConvert.DeserializeObject<MaterialsChangedMessage>(frame.Body);
							OnMaterialsChanged(materialsChangedMessage);
						}
						break;

					default:
						DebugUtils.Log(DebugContext.RestApi, $"Unhandled incoming message: {baseMessage.type}");
						break;
				}
			}
		};

		await websocket.Connect();
	}

	public void DispatchMessageQueue()
	{
		if (websocket != null)
		{
			websocket.DispatchMessageQueue();
		}
	}

	public async void Disconnect()
	{
		await websocket.Close();
		websocket = null;
	}

	private async void SendMessage(string destination, BaseMessage message)
	{
		if (websocket.State == WebSocketState.Open)
		{
			string json = JsonConvert.SerializeObject(message, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

			StompFrame frame = new(StompCommand.SEND, json);
			frame["content-type"] = "application/json";
			frame["content-length"] = json.Length.ToString();
			frame["destination"] = $"/app/collaboration/{collaborationId}{destination}";

			string payload = serializer.Serialize(frame);

			logger.Append($"Sending message: {payload}");

			await websocket.SendText(payload);

			logger.Append($"Sent command {frame.Command}: {message.GetType()} (sequence {message.sequence})");
		}

	}

	public void SendMessage(BaseMessage message)
	{
		string path = messageTypeToPath.GetValueOrDefault(message.GetType(), null);
		Assert.IsNotNull(path, $"Couldn't find a path for message type: {message.GetType()}");

		message.sequence = sequence++;

		SendMessage(path, message);
	}

}

}
