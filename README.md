# HTTPGateway

## What is it?

This is a server only mod for the game [Vintage Story][vs]. You must have purchased the game to use this mod. While this mod is server only, it will still work for single player games since the game runs a server locally.

This mod creates a webserver using the [EmbedIO][eio] library, enabling you to host webpages, images, or other static files. A basic API has been implemented as well, allowing you to fetch data from the game server to your site. Additionally, a websocket endpoint has been created, allowing you to connect and stream the game logs to your site. Included is basic server monitoring site built with Gridsome, a Vue static site generator. You can choose to replace all files in the Web directory if you would like to host your own site.

## How do I install it?

Until the game supports automatic importing of external references for mods, you will have to manually copy a few files as part of installation.

1. Extract your mod zip file, the extra files we need are located inside.
2. Copy the contents of the `/Lib/` folder to the `/Lib/` folder in your game directory, alongside the game executable.
3. Copy the `/Web/` directory to your `data` folder. This will be a new directory, and will be where you can add files to be hosted by the webserver.

## WARNING!

This mod relies on 3rd party software and opens up HTTP access to the server hosting your game. There are always security risks involved. This mod may also cause performance issues with your game under heavy load, please use caution and consider using a reverse proxy such as [NGINX][ng] if you plan on allowing public access to the site.

## Customization

The default port the server runs on is 80, enabling you to directly visit your server address in a web browser to open the hosted site. If you cannot bind to port 80, you can edit the file `httpgateway-port.txt` located in your vintage story game directory. You must load the mod at least once for this file to appear. The file should contain one line with the desired port. After changing the file, restart your server for the changes to apply.

## How do I use it?

Once the mod is installed and hosting, you can visit the default site by visiting the bound address in your web browser. If you would like to develop your own site, there is a built in authentication supplying a jwt token, and several endpoints providing useful information once you are authorized. The authorization is based on a 6-character code challenge that you can generate in game with the command `/httpgateway code`.

- POST`/api/login?code=<6-character-code>` returns `{ "_auth": "<jwt>" }`
- GET`/api/server` with header: `_auth`=`<jwt>` returns `<JSON of IServerAPI>` [IServerAPI][isrv]
- ws://`/ws/<jwt>` opens websocket connection which streams server logs in format: `{ "type": <logType>, "data": <logMessage> }`

The JWT secret is generated on first run and saved in the file `httpgateway.key`.  If you would like to revoke all token's access, just delete this file and the server will generate another.

A 6 character challenge code is automatically generated on startup and displayed in the server logs.  This can allow you to gain access without using the game client if needed.

## Todo

- Fetch existing logs on ServerLogs connect
- Better realtime updates, probably over websocket
- More endpoints for more stuff
- Interact with server chat over websocket

## Roadmap Ideas

- Add DB for more flexible persistance
- Full server management (over websocket and endpoint)
- Role based permissions (only admin for now)
- Reverse proxy support

[vs]: https://www.vintagestory.at/
[eio]: https://github.com/unosquare/embedio
[ng]: https://www.nginx.com/
[isrv]: http://apidocs.vintagestory.at/api/Vintagestory.API.Server.IServerAPI.html
