# SignalRCore client for Unity3D
Signal R Core client for Unity 3D to connect to ASP.NET CORE 3.1 server.
>  **The library is under testing!**

## How to start
- [download this DLL](https://yadi.sk/d/W8PQvLrnDKl16g)
- add Dll to the Unity folder, for example, "Assets"

## How to use
Simple client example ([for this server](https://yadi.sk/d/W8PQvLrnDKl16g)):
```csharp
public async Task SimpleExample()
{
    var client = new SignalRClientBuilder().WithUrl("wss://localhost:5001/signalr-hub") // url for websocket protocol (with SSL). Without SSL: "ws://localhost:5000/signalr-hub"
                                           .Build();

    client.On("MessageFromServer", (string message) => Debug.WriteLine($"Received: {message}"));

    await client.ConnectToServer();

    await client.SendMessage("MessageFromClient", "Hello World");
}
```


